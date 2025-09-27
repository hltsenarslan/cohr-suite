using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace File.Api;

public sealed class SignedUrlService(IConfiguration cfg)
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(
        cfg["Files:SigningKey"] ?? throw new InvalidOperationException("Files:SigningKey missing"));

    public string CreateToken(Guid fileId, Guid tenantId, TimeSpan lifetime)
    {
        var exp = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        var payload = $"{fileId:N}|{tenantId:N}|{exp}";
        var sig = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload));
        return $"{WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payload))}.{WebEncoders.Base64UrlEncode(sig)}";
    }

    public bool TryValidate(string token, Guid fileId, Guid tenantId, out string? error)
    {
        error = null;
        var parts = token.Split('.', 2);
        if (parts.Length != 2)
        {
            error = "format";
            return false;
        }

        var payloadBytes = WebEncoders.Base64UrlDecode(parts[0]);
        var payload = Encoding.UTF8.GetString(payloadBytes);
        var sigBytes = WebEncoders.Base64UrlDecode(parts[1]);

        var expected = HMACSHA256.HashData(_key, payloadBytes);
        if (!CryptographicOperations.FixedTimeEquals(expected, sigBytes))
        {
            error = "sig";
            return false;
        }

        var sp = payload.Split('|');
        if (sp.Length != 3)
        {
            error = "payload";
            return false;
        }

        if (!Guid.TryParse(sp[0], out var fid) || fid != fileId)
        {
            error = "file";
            return false;
        }

        if (!Guid.TryParse(sp[1], out var tid) || tid != tenantId)
        {
            error = "tenant";
            return false;
        }

        var exp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(sp[2]));
        if (DateTimeOffset.UtcNow > exp)
        {
            error = "expired";
            return false;
        }

        return true;
    }
}