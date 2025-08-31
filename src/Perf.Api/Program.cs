using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using Perf.Api.Infrastructure;
using Perf.Api.Endpoints;
using Perf.Api.Tenancy;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

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
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", p => p.RequireRole("admin"));
    options.AddPolicy("ViewerOrAdmin", p => p.RequireRole("admin", "viewer"));;
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

var app = builder.Build();

app.Use(async (ctx, next) =>
{
    // Perf/Comp path’lerinde çalışsın:
    var p = ctx.Request.Path.Value?.ToLowerInvariant() ?? "/";
    if (!p.StartsWith("/api/perf") && !p.StartsWith("/api/comp"))
    {
        await next();
        return;
    }

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
    var db = scope.ServiceProvider.GetRequiredService<PerfDbContext>();
    db.Database.Migrate(); // prod/dev’de migration
}


app.MapPerfHealthEndpoints();
app.UseMiddleware<TenantContextMiddleware>();
app.MapPerfMeEndpoints();
app.MapTestEndpoints();


app.Run();

public partial class Program
{
}