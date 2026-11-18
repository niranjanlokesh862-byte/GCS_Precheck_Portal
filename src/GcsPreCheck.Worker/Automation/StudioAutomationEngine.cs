using System.Security.Cryptography;
using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Application.Features.RulesEngine;
using GcsPreCheck.Domain.Entities;
using GcsPreCheck.Domain.Enums;
using GcsPreCheck.Domain.Services;
using GcsPreCheck.Worker.Safeguards;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace GcsPreCheck.Worker.Automation;

public record StudioInspectionResult(
    OverallResult OverallResult,
    IReadOnlyList<ParameterCheckResult> ParameterResults,
    string EvidenceDirectory);

public interface IStudioAutomationEngine
{
    Task<StudioInspectionResult> ExecuteInspectionAsync(
        string studioUrl,
        string username,
        char[] passwordBuffer,
        PreCheckRun run,
        Func<string, Task<string>> onMfaChallengeRequired,
        CancellationToken ct = default);
}

public class StudioAutomationEngine : IStudioAutomationEngine
{
    private readonly IApplicationDbContext _context;
    private readonly ISopRuleEvaluationEngine _ruleEngine;
    private readonly IReadOnlyRouteInterceptor _routeInterceptor;
    private readonly ILogger<StudioAutomationEngine> _logger;

    public StudioAutomationEngine(
        IApplicationDbContext context,
        ISopRuleEvaluationEngine ruleEngine,
        IReadOnlyRouteInterceptor routeInterceptor,
        ILogger<StudioAutomationEngine> logger)
    {
        _context = context;
        _ruleEngine = ruleEngine;
        _routeInterceptor = routeInterceptor;
        _logger = logger;
    }

    public async Task<StudioInspectionResult> ExecuteInspectionAsync(
        string studioUrl,
        string username,
        char[] passwordBuffer,
        PreCheckRun run,
        Func<string, Task<string>> onMfaChallengeRequired,
        CancellationToken ct = default)
    {
        using var playwright = await Playwright.CreateAsync();

        // 1. Launch Isolated Chromium Browser Instance
        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[]
            {
                "--no-sandbox",
                "--disable-gpu",
                "--disable-dev-shm-usage",
                "--disable-extensions",
                "--proxy-server=http://127.0.0.1:8888" // Route strictly through LocalStudioForwardProxy
            }
        };

        await using var browser = await playwright.Chromium.LaunchAsync(launchOptions);

        // 2. Configure Isolated Context with Strict Network Quarantine
        var contextOptions = new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = false, // Strictly validate TLS certificates
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
        };

        var context = await browser.NewContextAsync(contextOptions);

        // 3. Attach Intelligent Read-Only Safety Route Interceptor
        await context.RouteAsync("**/*", async route =>
        {
            await _routeInterceptor.InterceptRouteAsync(route);
        });

        var page = await context.NewPageAsync();

        // Create evidence storage directory for this run
        var evidenceDir = !string.IsNullOrWhiteSpace(run.EvidenceStorageDirectory)
            ? run.EvidenceStorageDirectory
            : Path.Combine(AppContext.BaseDirectory, "Storage", "Evidence", run.Id.ToString("N"));
        Directory.CreateDirectory(evidenceDir);

        try
        {
            _logger.LogInformation("Navigating to Studio URL: {StudioUrl}", studioUrl);
            await page.GotoAsync(studioUrl, new PageGotoOptions { Timeout = 30000, WaitUntil = WaitUntilState.DOMContentLoaded });

            // Fill login credentials
            var userField = page.Locator("#txtUsername, input[name='username'], input[type='text']").First;
            if (await userField.CountAsync() > 0 && await userField.IsVisibleAsync())
            {
                await userField.FillAsync(username);

                var passField = page.Locator("#txtPassword, input[name='password'], input[type='password']").First;
                var passwordString = new string(passwordBuffer);
                try
                {
                    await passField.FillAsync(passwordString);
                }
                finally
                {
                    // Clean memory buffer immediately
                    CryptographicOperations.ZeroMemory(System.Runtime.InteropServices.MemoryMarshal.AsBytes(passwordBuffer.AsSpan()));
                }

                var submitBtn = page.Locator("#btnLogin, button[type='submit'], input[type='submit']").First;
                await submitBtn.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            }

            // Check for MFA Challenge screen
            var mfaInput = page.Locator("#txtOtp, input[name='mfaCode'], input[name='otp']").First;
            if (await mfaInput.CountAsync() > 0 && await mfaInput.IsVisibleAsync())
            {
                _logger.LogInformation("MFA Challenge encountered for Studio run {RunId}", run.Id);
                var otp = await onMfaChallengeRequired("TOTP_6_DIGIT");
                await mfaInput.FillAsync(otp);
                var verifyBtn = page.Locator("#btnVerifyMfa, button:has-text('Verify')").First;
                await verifyBtn.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            }

            // Capture landing screenshot evidence
            var landingScreenshot = Path.Combine(evidenceDir, "01_Landing.png");
            await page.ScreenshotAsync(new() { Path = landingScreenshot });

            // Read all 10 parameter values from Studio page DOM (returns empty string if element absent)
            var siteVer = await ReadElementTextAsync(page, "#lblSiteVersion, .system-version-info");
            var chemVer = await ReadElementTextAsync(page, "#lblChemAdvisor, .chem-advisor-ver");
            var transDbVer = await ReadElementTextAsync(page, "#lblTransportDb, .transport-db-ver");
            var contentVer = await ReadElementTextAsync(page, "#lblContentVersion, .content-ver");
            var regionsCount = await ReadElementTextAsync(page, "#lblRegionsCount, .regions-count");
            var japanRegion = await ReadElementTextAsync(page, "#lblJapanRegion, .japan-status");
            var transportation = await ReadElementTextAsync(page, "#lblTransportation, .transportation-status");
            var pcnStatus = await ReadElementTextAsync(page, "#lblPcnStatus, .pcn-status");
            var customization = await ReadElementTextAsync(page, "#lblCustomization, .customization-status");

            // Query verified ticket requirements
            var ticketRequirements = await _context.TicketRequirementItems
                .AsNoTracking()
                .Where(r => r.TicketId == run.TicketId)
                .ToDictionaryAsync(r => r.ParameterCode.ToUpperInvariant(), r => r.EmployeeVerifiedValue ?? r.ExtractedRawText, ct);

            string GetExpected(string code) =>
                ticketRequirements.TryGetValue(code.ToUpperInvariant(), out var val) && !string.IsNullOrWhiteSpace(val) ? val : string.Empty;

            var inputs = new List<ParameterEvaluationInput>
            {
                new("SITE_VERSION", "Site version / Patch", GetExpected("SITE_VERSION"), siteVer, landingScreenshot),
                new("CHEMADVISOR_VERSION", "ChemAdvisor version", GetExpected("CHEMADVISOR_VERSION"), chemVer, landingScreenshot),
                new("TRANSPORT_DB_VERSION", "Transport database version", GetExpected("TRANSPORT_DB_VERSION"), transDbVer, landingScreenshot),
                new("CURRENT_CONTENT_VERSION", "Current content version", GetExpected("CURRENT_CONTENT_VERSION"), contentVer, landingScreenshot),
                new("REQUESTED_CONTENT_VERSION", "Requested content version", GetExpected("REQUESTED_CONTENT_VERSION"), contentVer, landingScreenshot),
                new("NUMBER_OF_REGIONS", "Number of regions", GetExpected("NUMBER_OF_REGIONS"), regionsCount, landingScreenshot),
                new("JAPAN_REGION", "Japan region", GetExpected("JAPAN_REGION"), japanRegion, landingScreenshot),
                new("TRANSPORTATION", "Transportation", GetExpected("TRANSPORTATION"), transportation, landingScreenshot),
                new("PCN", "PCN", GetExpected("PCN"), pcnStatus, landingScreenshot),
                new("CUSTOMER_CUSTOMIZATION", "Customer customization", GetExpected("CUSTOMER_CUSTOMIZATION"), customization, landingScreenshot)
            };

            var results = await _ruleEngine.EvaluateAllParametersAsync(run.Id, inputs, ct);
            var overall = OverallStatusAggregator.Aggregate(results);

            return new StudioInspectionResult(overall, results, evidenceDir);
        }
        finally
        {
            // Close and destroy isolated browser context permanently
            await context.CloseAsync();
        }
    }

    private static async Task<string> ReadElementTextAsync(IPage page, string selector)
    {
        try
        {
            var locator = page.Locator(selector).First;
            if (await locator.CountAsync() > 0 && await locator.IsVisibleAsync())
            {
                var text = await locator.InnerTextAsync();
                if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
            }
        }
        catch { }

        return string.Empty;
    }
}
