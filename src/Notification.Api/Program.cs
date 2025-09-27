using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.AddOpenTelemetry().WithTracing(t =>
{
    t.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("notification-api"))
        .AddAspNetCoreInstrumentation().AddHttpClientInstrumentation()
        .AddOtlpExporter(o =>
            o.Endpoint = new Uri(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ??
                                 "http://jaeger:4317"));
});

var jwt = cfg.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new()
    {
        ValidIssuer = jwt["Issuer"],
        ValidAudience = jwt["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Secret"]!)),
        ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true, ValidateLifetime = true
    };
});
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { ok = true, svc = "notification-api" }));

app.UseAuthentication();
app.UseAuthorization();

// basit iskelet: log’a yaz
app.MapPost("/api/notify", (HttpContext ctx, NotifyReq req, ILoggerFactory lf) =>
{
    if (!Guid.TryParse(ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault(), out var tenantId))
        return Results.BadRequest(new { error = "tenant_missing" });

    var log = lf.CreateLogger("Notify");
    log.LogInformation("NOTIFY tenant={Tenant} channel={Channel} to={To} template={Template} payload={Payload}",
        tenantId, req.channel, req.to, req.template, req.payload);

    return Results.Accepted();
}).RequireAuthorization();

app.Run(cfg["ASPNETCORE_URLS"]);

public record NotifyReq(string channel, string to, string template, Dictionary<string, object>? payload);