using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Notification.Api.Infrastructure;
using Notification.Api.Services;
using Microsoft.OpenApi.Models;
using Notification.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// DB
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<NotifDbContext>(opt =>
    {
        var cs = builder.Configuration.GetConnectionString("Default")
                 ?? "Host=notif-db;Database=notif;Username=postgres;Password=postgres";
        opt.UseNpgsql(cs);
    });

    builder.Services.AddHttpClient("file", c => { c.Timeout = TimeSpan.FromSeconds(100); });
}


// services
builder.Services.AddScoped<IMailer, Mailer>();
builder.Services.AddHostedService<QueueWorker>();
builder.Services.AddSingleton<QueueWorker>();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// migrate
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotifDbContext>();
    db.Database.Migrate();
}

app.MapMailEndpoints();


app.Run();

public record EnqueueMailReq(Guid TenantId, string[] To, string Subject, string HtmlBody, Guid[]? AttachmentFileIds);

public sealed class IntrospectReply
{
    public bool Revoked { get; set; }
}

public partial class Program
{
}