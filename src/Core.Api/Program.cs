using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Core.Api.Infrastructure;
using Core.Api.Endpoints;
using Core.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);


builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<CoreDbContext>(o =>
        o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
}

builder.Services.AddScoped<IFeatureGate, FeatureGate>();
builder.Services.AddSingleton<ILicenseCache, LicenseCache>();

builder.Services.AddOpenTelemetry()
    .WithTracing(t =>
    {
        t.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("core-api"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o =>
            {
                var ep = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://jaeger:4317";
                o.Endpoint = new Uri(ep);
            });
    });


var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Secret"]!)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role
        };


        o.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var db = ctx.HttpContext.RequestServices.GetRequiredService<CoreDbContext>();
                var jti = ctx.Principal?.FindFirst("jti")?.Value;

                if (!string.IsNullOrEmpty(jti))
                {
                    var revoked = await db.RevokedAccessTokens.AnyAsync(x => x.Jti == jti);
                    if (revoked)
                    {
                        ctx.Fail("Access token revoked");
                    }
                }
            },
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
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = builder.Environment.ApplicationName, Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
                { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddAuthorization(o => o.AddPolicy("RequireAdmin", p => p.RequireRole("admin")));

var app = builder.Build();


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
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    db.Database.Migrate();
}

using (var scope = app.Services.CreateScope())
{
    var lic = scope.ServiceProvider.GetRequiredService<ILicenseCache>();
    await lic.ReloadAsync();
}

app.MapInternalFeatureEndpoints();
app.MapHealthEndpoints();
app.MapAuth(app.Configuration, app.Environment);
app.MapInternalDomainEndpoints();
app.MapInternalTenantEndpoints();
app.MapAdminTenantEndpoints();
app.MapAdminDomainEndpoints();
app.MapAdminSubscriptionEndpoints();
app.MapAdmin();

var urls = builder.Configuration["ASPNETCORE_URLS"];
app.Run(urls);

public partial class Program
{
}