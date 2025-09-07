using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Core.Api.Domain;
using Core.Api.Helpers;
using Core.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Core.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder app, IConfiguration cfg, IWebHostEnvironment env)
    {
        
        app.MapPost("/internal/auth/introspect", async (
                CoreDbContext db,
                IntrospectRequest req) =>
            {
                if (string.IsNullOrWhiteSpace(req.Jti))
                    return Results.BadRequest(new { error = "missing_jti" });

                var revoked = await db.RevokedAccessTokens
                    .AnyAsync(x => x.Jti == req.Jti && x.RevokedAt != null);

                return Results.Ok(new IntrospectResponse { Revoked = revoked });
            })
            .WithTags("internal")
            .WithName("InternalAuthIntrospect");
        
        app.MapPost("/auth/login", async (LoginRequest req, CoreDbContext db,
            IHttpContextAccessor http) =>
        {
            var user = await db.Users
                .Include(u => u.UserTenants).ThenInclude(ut => ut.Role)
                .FirstOrDefaultAsync(u => u.Email == req.Email && u.IsActive);
            if (user is null) return Results.Unauthorized();

            Guid? tenantId = null;
            if (!string.IsNullOrWhiteSpace(req.Tenant))
            {
                tenantId = await db.Tenants
                    .Where(t => t.Slug == req.Tenant)
                    .Select(t => (Guid?)t.Id)
                    .FirstOrDefaultAsync();

                if (tenantId is null) return Results.BadRequest(new { error = "tenant_not_found" });
            }

            var passOk = BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash)
                         || (env.IsDevelopment() && user.PasswordHash == req.Password);
            if (!passOk) return Results.Unauthorized();

            var roles = user.UserTenants
                .Where(ut => tenantId == null || ut.TenantId == tenantId.Value)
                .Select(ut => ut.Role!.Name)
                .Distinct()
                .ToArray();

            var jwtSecret = cfg["Jwt:Secret"]!;
            var accessTtl = TimeSpan.FromMinutes(int.Parse(cfg["Jwt:AccessTtlMinutes"] ?? "15"));
            var refreshTtl = TimeSpan.FromDays(int.Parse(cfg["Jwt:RefreshTtlDays"] ?? "7"));

            var accessToken = CreateJwt(user.Id,
                user.Email,
                tenantId,
                roles,
                cfg["Jwt:Issuer"]!,
                cfg["Jwt:Audience"]!,
                cfg["Jwt:Secret"]!,
                accessTtl);

            var rawRefresh = RandomBase64Url.Create(32);
            var hash = Sha256Hex.Create(rawRefresh);
            var rt = new RefreshToken
            {
                UserId = user.Id,
                TenantId = tenantId,
                TokenHash = hash,
                ExpiresAt = DateTime.UtcNow.Add(refreshTtl),
                CreatedByIp = http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = http.HttpContext?.Request.Headers.UserAgent.ToString()
            };
            db.Add(rt);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                accessToken,
                expiresIn = (int)accessTtl.TotalSeconds,
                refreshToken = rawRefresh
            });
        });

        app.MapPost("/auth/refresh", async (
            RefreshRequest req,
            CoreDbContext db,
            IConfiguration cfg,
            IHttpContextAccessor http) =>
        {
            if (string.IsNullOrWhiteSpace(req.RefreshToken))
                return Results.BadRequest(new { error = "invalid_refresh" });

            var hash = Sha256Hex.Create(req.RefreshToken);
            var rt = await db.RefreshTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.TokenHash == hash);

            if (rt is null || rt.RevokedAt != null || rt.ExpiresAt < DateTime.UtcNow)
                return Results.Unauthorized();

            if (!rt.User.IsActive) return Results.Unauthorized();

            var ut = await db.UserTenants
                .Include(x => x.Role)
                .Where(x => x.UserId == rt.UserId && (rt.TenantId == null || x.TenantId == rt.TenantId))
                .ToListAsync();

            var roles = ut.Select(x => x.Role.Name).Distinct().ToArray();
            Guid? tenantId = rt.TenantId ?? ut.Select(x => x.TenantId).FirstOrDefault();

            var accessTtl = TimeSpan.FromMinutes(int.Parse(cfg["Jwt:AccessTtlMinutes"] ?? "15"));
            var accessToken = CreateJwt(rt.UserId,
                rt.User.Email,
                tenantId,
                roles,
                cfg["Jwt:Issuer"]!,
                cfg["Jwt:Audience"]!,
                cfg["Jwt:Secret"]!,
                accessTtl);

            var rawRefresh = RandomBase64Url.Create(32);
            var newHash = Sha256Hex.Create(rawRefresh);
            rt.RevokedAt = DateTime.UtcNow;
            rt.ReplacedByTokenHash = newHash;

            var newRt = new RefreshToken
            {
                UserId = rt.UserId,
                TenantId = tenantId,
                TokenHash = newHash,
                ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(cfg["Jwt:RefreshTtlDays"] ?? "7")),
                CreatedByIp = http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = http.HttpContext?.Request.Headers.UserAgent.ToString()
            };

            db.Add(newRt);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                accessToken,
                expiresIn = (int)accessTtl.TotalSeconds,
                refreshToken = rawRefresh
            });
        });

        app.MapPost("/auth/logout", async (
            LogoutRequest req,
            HttpContext ctx,
            CoreDbContext db) =>
        {
            if (!string.IsNullOrWhiteSpace(req.RefreshToken))
            {
                var hash = Sha256Hex.Create(req.RefreshToken);
                var rt = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash);
                if (rt is not null && rt.RevokedAt is null)
                {
                    rt.RevokedAt = DateTime.UtcNow;
                }
            }

            var jti = ctx.User?.FindFirst("jti")?.Value;
            if (!string.IsNullOrEmpty(jti))
            {
                var exists = await db.RevokedAccessTokens.AnyAsync(x => x.Jti == jti);
                if (!exists)
                {
                    db.RevokedAccessTokens.Add(new RevokedAccessToken
                    {
                        Jti = jti,
                        RevokedAt = DateTime.UtcNow
                    });
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok();
        });

        string CreateJwt(
            Guid userId,
            string email,
            Guid? tenantId,
            string[] roles,
            string issuer, string audience, string secret, TimeSpan ttl)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new("email", email)
            };

            if (tenantId is Guid t)
                claims.Add(new("tenantId", t.ToString()));

            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            var keyBytes = Encoding.UTF8.GetBytes(secret);
            var key = new SymmetricSecurityKey(keyBytes);
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var jwt = new JwtSecurityToken(
                issuer: cfg["Jwt:Issuer"],
                audience: cfg["Jwt:Audience"],
                claims: claims,
                notBefore: DateTime.UtcNow, expires: DateTime.UtcNow.Add(ttl),
                signingCredentials: creds
            );
            return new JwtSecurityTokenHandler().WriteToken(jwt);
        }
    }

    public record LoginRequest(string Email, string Password, string? Tenant);

    public record RefreshRequest(string RefreshToken);

    public record LogoutRequest(string RefreshToken);
    
    public record IntrospectRequest(string Jti);
    public record IntrospectResponse { public bool Revoked { get; set; } }
}