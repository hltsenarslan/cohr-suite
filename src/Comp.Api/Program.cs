using System.IdentityModel.Tokens.Jwt;
using Comp.Api.Endpoints;
using Comp.Api.Infrastructure;
using Comp.Api.Tenancy;
using Microsoft.EntityFrameworkCore;
using ITenantContext = Comp.Api.Tenancy.ITenantContext;
using TenantContext = Comp.Api.Tenancy.TenantContext;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<ITenantContext, TenantContext>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<CompDbContext>(opt =>
    {
        var cs = builder.Configuration.GetConnectionString("Default")
                 ?? "Host=comp-db;Database=comp;Username=postgres;Password=postgres";
        opt.UseNpgsql(cs);
    });
}

var jwt = builder.Configuration.GetSection("Jwt");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SigningKey"]!)),
            ValidateIssuer = true, ValidateAudience = true,
            ValidateIssuerSigningKey = true, ValidateLifetime = true,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    // JWT için “Authorize” butonu
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", p => p.RequireRole("admin"));
});


var app = builder.Build();

app.Use(async (ctx, next) =>
{
    // Perf/Comp path’lerinde çalışsın:
    var p = ctx.Request.Path.Value?.ToLowerInvariant() ?? "/";
    if (!p.StartsWith("/api/perf") && !p.StartsWith("/api/comp"))
    { await next(); return; }

    // Token’daki tenant
    var claimTenant = ctx.User?.FindFirst("tenantId")?.Value;
    // Header belirleyelim (Gateway zaten yazıyor)
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
        c.RoutePrefix = "swagger"; // /swagger
    });
}

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CompDbContext>();
    db.Database.Migrate(); // prod/dev’de migration
}

app.MapHealth();

app.UseMiddleware<TenantContextMiddleware>();
app.MapMe();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CompDbContext>();
    db.Database.Migrate();

    var firm1 = Guid.Parse("a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac");
    var firm2 = Guid.Parse("44709835-d55a-ef2a-2327-5fdca19e55d8");

    if (!db.Salaries.Any())
    {
        db.Salaries.AddRange(
            new Salary { TenantId = firm1, Employee = "U1", Amount = 1000, Period = new DateOnly(2025, 1, 1) },
            new Salary { TenantId = firm1, Employee = "U1", Amount = 1100, Period = new DateOnly(2025, 2, 1) },
            new Salary { TenantId = firm2, Employee = "V1", Amount = 2000, Period = new DateOnly(2025, 1, 1) }
        );
        db.SaveChanges();
    }
}

app.Run();

public partial class Program
{
}