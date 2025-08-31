using Core.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

public record LoginRequest(string Email, string Password, string? Tenant);
public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder app, IConfiguration cfg, IWebHostEnvironment env)
    {
        app.MapPost("/auth/login", async (LoginRequest req, CoreDbContext db) =>
        {
            // 1) kullanıcı
            var user = await db.Users
                .Include(u => u.UserTenants).ThenInclude(ut => ut.Role)
                .FirstOrDefaultAsync(u => u.Email == req.Email && u.IsActive);
            if (user is null) return Results.Unauthorized();

            // 2) tenant çöz
            Guid? tenantId = null;
            if (!string.IsNullOrWhiteSpace(req.Tenant))
            {
                tenantId = await db.Tenants
                    .Where(t => t.Slug == req.Tenant)
                    .Select(t => (Guid?)t.Id)
                    .FirstOrDefaultAsync();

                if (tenantId is null) return Results.BadRequest(new { error = "tenant_not_found" });
            }

            // 3) parolayı doğrula (dev fallback)
            var passOk = BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash)
                         || (env.IsDevelopment() && user.PasswordHash == req.Password);
            if (!passOk) return Results.Unauthorized();

            // 4) roller (tenant seçildiyse o tenant'a göre süz)
            var roles = user.UserTenants
                .Where(ut => tenantId == null || ut.TenantId == tenantId.Value)
                .Select(ut => ut.Role!.Name)
                .Distinct()
                .ToArray();

            // 5) JWT
            var secret = cfg["Jwt:Secret"];
            var token = CreateJwt(user.Id, user.Email, tenantId, roles, secret, TimeSpan.FromHours(8));

            return Results.Ok(new { token, sub = user.Id, email = user.Email, tenantId, roles });
        });
    }

    static string CreateJwt(Guid userId, string email, Guid? tenantId, string[] roles, string secret, TimeSpan ttl)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new("sub", userId.ToString()),
            new("email", email)
        };
        if (tenantId is Guid t) claims.Add(new("tenantId", t.ToString()));
        claims.AddRange(roles.Select(r => new System.Security.Claims.Claim("role", r)));

        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secret));
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.Add(ttl),
            signingCredentials: creds
        );
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(jwt);
    }
}