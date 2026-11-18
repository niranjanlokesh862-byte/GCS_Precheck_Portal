using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace GcsPreCheck.Worker.Safeguards;

public interface IReadOnlyRouteInterceptor
{
    Task InterceptRouteAsync(IRoute route);
}

public class ReadOnlyRouteInterceptor : IReadOnlyRouteInterceptor
{
    private static readonly string[] MutationKeywords =
    [
        "save", "update", "delete", "apply", "edit", "patch", "import", "submit", "execute", "modify", "create", "alter"
    ];

    private static readonly string[] ApprovedAuthPathKeywords =
    [
        "login", "auth", "mfa", "token", "saml", "sso", "session"
    ];

    private readonly ILogger<ReadOnlyRouteInterceptor> _logger;

    public ReadOnlyRouteInterceptor(ILogger<ReadOnlyRouteInterceptor> logger)
    {
        _logger = logger;
    }

    public async Task InterceptRouteAsync(IRoute route)
    {
        var request = route.Request;
        var method = request.Method.ToUpperInvariant();
        var url = request.Url;

        // 1. Safe Read-Only Verbs
        if (method is "GET" or "HEAD" or "OPTIONS")
        {
            await route.ContinueAsync();
            return;
        }

        // 2. Strictly Block Destructive Verbs
        if (method is "PUT" or "DELETE" or "PATCH")
        {
            _logger.LogWarning("READ-ONLY SAFETY GUARD: Aborted {Method} request to '{Url}'", method, url);
            await route.AbortAsync(RequestAbortErrorCode.AccessDenied);
            return;
        }

        // 3. Intelligent Inspection of POST Requests
        if (method == "POST")
        {
            var lowerUrl = url.ToLowerInvariant();

            // Check if this is an approved authentication / session request
            if (ApprovedAuthPathKeywords.Any(k => lowerUrl.Contains(k)))
            {
                await route.ContinueAsync();
                return;
            }

            // Check if URL targets mutating actions
            if (MutationKeywords.Any(k => lowerUrl.Contains(k)))
            {
                _logger.LogWarning("READ-ONLY SAFETY GUARD: Aborted mutating POST to '{Url}'", url);
                await route.AbortAsync(RequestAbortErrorCode.AccessDenied);
                return;
            }

            // Inspect Post Data for mutating keywords or ASP.NET WebForms postback
            var postData = request.PostData ?? string.Empty;
            if (postData.Contains("__VIEWSTATE", StringComparison.OrdinalIgnoreCase) &&
                !MutationKeywords.Any(k => postData.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                // Legitimate WebForms pagination / navigation postback
                await route.ContinueAsync();
                return;
            }

            // For other queries (e.g. read search queries)
            if (!MutationKeywords.Any(k => postData.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                await route.ContinueAsync();
                return;
            }

            _logger.LogWarning("READ-ONLY SAFETY GUARD: Aborted POST payload targeting mutation to '{Url}'", url);
            await route.AbortAsync(RequestAbortErrorCode.AccessDenied);
            return;
        }

        // Abort any unrecognized or unsupported HTTP verb
        _logger.LogWarning("READ-ONLY SAFETY GUARD: Aborted unrecognized verb {Method} to '{Url}'", method, url);
        await route.AbortAsync(RequestAbortErrorCode.AccessDenied);
    }
}
