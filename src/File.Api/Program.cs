using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using File.Api;
using File.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;


var builder = WebApplication.CreateBuilder(args);

var cfg = builder.Configuration;

// DB
builder.Services.AddDbContext<FilesDbContext>(o =>
    o.UseNpgsql(
        cfg.GetConnectionString("Default") ?? "Host=files-db;Database=files;Username=postgres;Password=postgres"));


// OTEL (opsiyonel)
builder.Services.AddOpenTelemetry().WithTracing(t =>
{
    t.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("file-api"))
        .AddAspNetCoreInstrumentation().AddHttpClientInstrumentation()
        .AddOtlpExporter(o =>
            o.Endpoint = new Uri(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ??
                                 "http://jaeger:4317"));
});

builder.Services.AddHttpClient("core", client =>
{
    var coreUrl = builder.Configuration["Core:BaseUrl"];
    if (string.IsNullOrEmpty(coreUrl))
        throw new InvalidOperationException("Missing Core:BaseUrl in config");
    client.BaseAddress = new Uri(coreUrl.TrimEnd('/'));
});

// JWT
var jwt = cfg.GetSection("Jwt");
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

builder.Services.AddSingleton<SignedUrlService>();

// Upload limit (MB)
var maxMb = int.TryParse(cfg["Files:MaxUploadMb"], out var mm) ? mm : 50;
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = (long)maxMb * 1024 * 1024);

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
    db.Database.Migrate();
}

app.MapGet("/health", () => Results.Ok(new { ok = true, svc = "file-api", ts = DateTime.UtcNow }));

app.UseAuthentication();
app.UseAuthorization();

static (Guid tenantId, IResult? problem) RequireTenant(HttpContext ctx)
{
    if (!Guid.TryParse(ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault(), out var tenantId))
        return (Guid.Empty, Results.BadRequest(new { error = "tenant_missing" }));
    return (tenantId, null);
}

string root = cfg["Files:Root"] ?? "/data/files";

// POST /api/files  (multipart: file, isSecure=true|false)
app.MapPost("/api/files",
        async (HttpContext ctx, FilesDbContext db, SignedUrlService url, [FromForm] FileUploadDto dto) =>
        {
            if (!ctx.User.Identity?.IsAuthenticated ?? true) return Results.Unauthorized();
            var (tenantId, problem) = RequireTenant(ctx);
            if (problem is not null) return problem;

            var secure = dto.isSecure ?? false;
            if (dto.file.Length == 0) return Results.BadRequest(new { error = "empty_file" });

            var now = DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.Combine(builder.Environment.ContentRootPath, "data", "files");
            }

            if(dto.isPublic == true)
                tenantId = Guid.Empty;
            
            var folder = Path.Combine(root, tenantId.ToString("N"), secure ? "secure" : "public",
                $"{now:yyyy}", $"{now:MM}", $"{now:dd}");
            Directory.CreateDirectory(folder);

            var id = Guid.NewGuid();
            var ext = Path.GetExtension(dto.file.FileName);
            var fname = $"{id:N}{ext}";
            var fullPath = Path.Combine(folder, fname);

            await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16384,
                             true))
            {
                await dto.file.CopyToAsync(fs, ctx.RequestAborted);
            }

            string rel = Path.GetRelativePath(root, fullPath).Replace('\\', '/');

            // hash
            string sha256;
            await using (var fs = System.IO.File.OpenRead(fullPath))
            {
                var hash = await SHA256.HashDataAsync(fs, ctx.RequestAborted);
                sha256 = Convert.ToHexString(hash).ToLowerInvariant();
            }

            
            
            var rec = new FileRecord
            {
                Id = id, TenantId = tenantId, IsSecure = secure, RelPath = rel,
                ContentType = dto.file.ContentType ?? "application/octet-stream",
                Size = dto.file.Length, Sha256 = sha256, OriginalName = dto.file.FileName
            };
            db.Files.Add(rec);
            await db.SaveChangesAsync(ctx.RequestAborted);

            var download = $"/api/files/{id}";
            return Results.Ok(new
            {
                id,
                secure,
                url = secure ? null : download,
                tokenUrl = secure ? $"/api/files/{id}/tokens" : null
            });
        })
    .DisableAntiforgery()
    .RequireAuthorization();

// GET /api/files/{id}?t=token
app.MapGet("/api/files/{id:guid}", async (HttpContext ctx, Guid id, FilesDbContext db, SignedUrlService svc) =>
{
    var (tenantId, problem) = RequireTenant(ctx);
    //if (problem is not null) return problem;

    var rec = await db.Files.FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId, ctx.RequestAborted);
    if (rec is null) return Results.NotFound();

    var full = Path.Combine(root, rec.RelPath);
    if (!System.IO.File.Exists(full)) return Results.NotFound(new { error = "file_missing" });

    if (rec.IsSecure)
    {
        var t = ctx.Request.Query["t"].ToString();
        if (string.IsNullOrEmpty(t)) return Results.Unauthorized();

        if (!svc.TryValidate(t, rec.Id, rec.TenantId, out var why))
            return Results.StatusCode(StatusCodes.Status401Unauthorized);
    }

    return Results.File(full, rec.ContentType, enableRangeProcessing: true);
});

// POST /api/files/{id}/tokens  { "expiresInSeconds": 300 }
app.MapPost("/api/files/{id:guid}/tokens", async (HttpContext ctx, Guid id, FilesDbContext db, SignedUrlService svc) =>
{
    if (!ctx.User.Identity?.IsAuthenticated ?? true) return Results.Unauthorized();
    var (tenantId, problem) = RequireTenant(ctx);
    if (problem is not null) return problem;

    var rec = await db.Files.FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId, ctx.RequestAborted);
    if (rec is null) return Results.NotFound();
    if (!rec.IsSecure) return Results.BadRequest(new { error = "not_secure" });

    var body = await ctx.Request.ReadFromJsonAsync<TokenReq>(cancellationToken: ctx.RequestAborted) ??
               new TokenReq(300);
    var token = svc.CreateToken(rec.Id, rec.TenantId,
        TimeSpan.FromSeconds(Math.Clamp(body.expiresInSeconds, 30, 3600)));

    var signedUrl = $"/api/files/{rec.Id}?t={token}";
    return Results.Ok(new { token, url = signedUrl });
}).RequireAuthorization();

app.MapDelete("/api/files/{id:guid}", async (HttpContext ctx, Guid id, FilesDbContext db) =>
{
    if (!ctx.User.Identity?.IsAuthenticated ?? true) return Results.Unauthorized();
    var (tenantId, problem) = RequireTenant(ctx);
    if (problem is not null) return problem;

    var rec = await db.Files.FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId, ctx.RequestAborted);
    if (rec is null) return Results.NotFound();

    var full = Path.Combine(root, rec.RelPath);
    if (System.IO.File.Exists(full)) System.IO.File.Delete(full);

    db.Files.Remove(rec);
    await db.SaveChangesAsync(ctx.RequestAborted);
    return Results.NoContent();
}).RequireAuthorization();

app.Run(cfg["ASPNETCORE_URLS"]);

record TokenReq(int expiresInSeconds);

public sealed record FileUploadDto(IFormFile file, bool? isSecure, bool? isPublic);

public sealed class IntrospectReply
{
    public bool Revoked { get; set; }
}