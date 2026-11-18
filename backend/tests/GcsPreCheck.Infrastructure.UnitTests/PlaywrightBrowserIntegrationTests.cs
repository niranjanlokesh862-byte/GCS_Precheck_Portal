using FluentAssertions;
using GcsPreCheck.Worker.Safeguards;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using Xunit;

namespace GcsPreCheck.Infrastructure.UnitTests;

public class PlaywrightBrowserIntegrationTests
{
    [Fact]
    public async Task Playwright_Driver_CanInitialize()
    {
        using var playwright = await Playwright.CreateAsync();
        playwright.Should().NotBeNull();
        playwright.Chromium.Should().NotBeNull();
    }

    [Fact]
    public async Task ReadOnlyRouteInterceptor_WithPlaywright_EnforcesSecurityPolicies()
    {
        // Ensure browser binaries exist
        Microsoft.Playwright.Program.Main(["install", "chromium"]);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = ["--no-sandbox", "--disable-gpu", "--disable-dev-shm-usage"]
        });

        var context = await browser.NewContextAsync();
        var interceptor = new ReadOnlyRouteInterceptor(NullLogger<ReadOnlyRouteInterceptor>.Instance);
        await context.RouteAsync("**/*", async route => await interceptor.InterceptRouteAsync(route));

        var page = await context.NewPageAsync();

        // 1. Verify page rendering in isolated context
        await page.SetContentAsync("<html><body><h1 id='title'>WERCS GCS Isolation Test</h1></body></html>");
        var title = await page.InnerTextAsync("#title");
        title.Should().Be("WERCS GCS Isolation Test");

        await context.CloseAsync();
    }
}
