using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace Core.Api.Infrastructure;

public static class AdminAuth
{
    /// <summary>
    /// appsettings (ve env) üzerinden admin API key okur.
    /// Varsayılan path: "Admin:ApiKey".
    /// Env fallback: ADMIN__APIKEY veya ADMIN_API_KEY
    /// </summary>
    public static RouteGroupBuilder UseAdminAuthFromConfig(
        this RouteGroupBuilder group,
        IConfiguration config,
        string keyPath = "Admin:ApiKey",
        string fallback = "dev-admin-key")
    {
        var key = config[keyPath];

        key ??= Environment.GetEnvironmentVariable("ADMIN__APIKEY");

        key ??= Environment.GetEnvironmentVariable("ADMIN_API_KEY");

        if (string.IsNullOrWhiteSpace(key)) key = fallback;

        group.AddEndpointFilter(new AdminKeyFilter(key));
        return group;
    }

    /// <summary>
    /// İstersen sabit bir anahtarla da kullan.
    /// </summary>
    public static RouteGroupBuilder UseAdminAuth(this RouteGroupBuilder group, string expectedKey)
    {
        group.AddEndpointFilter(new AdminKeyFilter(expectedKey));
        return group;
    }

    private sealed class AdminKeyFilter : IEndpointFilter
    {
        private readonly string _expected;
        public AdminKeyFilter(string expectedKey) => _expected = expectedKey;

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
        {
            var http = ctx.HttpContext;
            var key = http.Request.Headers["x-admin-key"].ToString();
            if (string.IsNullOrEmpty(key))
                key = http.Request.Query["x-admin-key"].ToString();

            if (!string.Equals(key, _expected, StringComparison.Ordinal))
                return Results.Unauthorized();

            return await next(ctx);
        }
    }
}