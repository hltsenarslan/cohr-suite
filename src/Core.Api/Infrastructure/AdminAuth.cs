// Infrastructure/AdminAuth.cs
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata; // IEndpointFilter
using Microsoft.AspNetCore.Routing;        // RouteGroupBuilder
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
        // 1) appsettings / secrets
        var key = config[keyPath];

        // 2) ASP.NET Core çift alt çizgi map’i: Admin:ApiKey -> ADMIN__APIKEY
        key ??= Environment.GetEnvironmentVariable("ADMIN__APIKEY");

        // 3) Düz env key (opsiyonel)
        key ??= Environment.GetEnvironmentVariable("ADMIN_API_KEY");

        // 4) Son çare: fallback
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
            // Header veya Query’den kabul et: x-admin-key
            var http = ctx.HttpContext;
            var key = http.Request.Headers["x-admin-key"].ToString();
            if (string.IsNullOrEmpty(key))
                key = http.Request.Query["x-admin-key"].ToString();

            if (!string.Equals(key, _expected, StringComparison.Ordinal))
                return Results.Unauthorized(); // 401

            return await next(ctx);
        }
    }
}