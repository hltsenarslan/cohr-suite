using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using Perf.Api.Infrastructure;
using Perf.Api.Endpoints;
using Perf.Api.Tenancy;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Perf.Api.CoreClient;
using Perf.Api.Enforcement;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<ITenantContext, TenantContext>();
if (!builder.Environment.IsEnvironment("Testing"))
{

    builder.Services.AddDbContext<PerfDbContext>(opt =>
    {
        var cs = builder.Configuration.GetConnectionString("Default")
                 ?? "Host=perf-db;Database=perf;Username=postgres;Password=postgres";
        opt.UseNpgsql(cs);
    });

    builder.Services.AddHttpClient<ICoreFeatureClient, CoreFeatureClient>(client =>
    {
        var coreUrl = builder.Configuration["Core:BaseUrl"];
        if (string.IsNullOrEmpty(coreUrl))
            throw new InvalidOperationException("Missing Core:BaseUrl in config");
        client.BaseAddress = new Uri(coreUrl.TrimEnd('/'));
    });
}


var jwt = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwt["Issuer"],
            ValidateAudience = true, ValidAudience = jwt["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Secret"]!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role
        };

        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var auth = ctx.Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrWhiteSpace(auth))
                {
                    auth = auth.Trim().Trim('"').Trim('\'');
                    if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        ctx.Token = auth.Substring("Bearer ".Length).Trim();
                }

                return Task.CompletedTask;
            },

            OnTokenValidated = async ctx =>
            {
                var http = ctx.HttpContext.RequestServices
                    .GetRequiredService<IHttpClientFactory>()
                    .CreateClient("core");

                var jti = ctx.Principal?.FindFirst("jti")?.Value;
                if (!string.IsNullOrEmpty(jti))
                {
                    var payload = new { jti };
                    using var res = await http.PostAsJsonAsync("/internal/auth/introspect", payload,
                        ctx.HttpContext.RequestAborted);
                    if (!res.IsSuccessStatusCode)
                    {
                        ctx.Fail("introspection_failed");
                        return;
                    }

                    var body = await res.Content.ReadFromJsonAsync<IntrospectReply>(
                        cancellationToken: ctx.HttpContext.RequestAborted);
                    if (body?.Revoked == true)
                    {
                        ctx.Fail("access_token_revoked");
                        return;
                    }
                }
            },

            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine($"[JWT][AuthFailed] {ctx.Exception.GetType().Name}: {ctx.Exception.Message}");
                return Task.CompletedTask;
            },

            OnChallenge = ctx =>
            {
                Console.WriteLine($"[JWT][Challenge] Error={ctx.Error} Desc={ctx.ErrorDescription}");
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", p => p.RequireRole("admin"));
    options.AddPolicy("ViewerOrAdmin", p => p.RequireRole("admin", "viewer"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = builder.Environment.ApplicationName, Version = "v1" });
    o.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Bearer {token}"
    });
    o.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<PerfFeatureMiddleware>();

app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value?.ToLowerInvariant() ?? "/";
    if (!p.StartsWith("/api/perf") && !p.StartsWith("/api/comp"))
    {
        await next();
        return;
    }

    var claimTenant = ctx.User?.FindFirst("tenantId")?.Value;
    var headerTenant = ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault();

    if (!string.IsNullOrEmpty(claimTenant) && !string.IsNullOrEmpty(headerTenant) &&
        !string.Equals(claimTenant, headerTenant, StringComparison.OrdinalIgnoreCase))
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        await ctx.Response.WriteAsJsonAsync(new { error = "tenant_mismatch" });
        return;
    }

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{app.Environment.ApplicationName} v1");
        c.RoutePrefix = "swagger";
    });
}

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PerfDbContext>();
    db.Database.Migrate();
}

app.MapPerfHealthEndpoints();
app.UseMiddleware<TenantContextMiddleware>();
app.UseMiddleware<PerfFeatureMiddleware>();
app.MapPerfMeEndpoints();
app.MapTestEndpoints();

app.Run();

public partial class Program
{
}

public sealed class IntrospectReply
{
    public bool Revoked { get; set; }
}