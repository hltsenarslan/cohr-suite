// Core.Api/Services/LicenseLoader.cs

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Api.Domain;

namespace Core.Api.Services;

public static class LicenseLoader
{
    record Envelope(
        string alg,
        string sigAlg,
        string kdf,
        string salt,
        string iv,
        string ciphertext,
        string tag,
        string signature);

    public static LicenseSnapshot LoadAndValidate(string rootPath, IConfiguration cfg, ILogger logger)
    {
        var path = Path.Combine(rootPath, "license.lic");
        if (!File.Exists(path)) throw new FileNotFoundException("license.lic not found", path);

        var json = File.ReadAllText(path);
        var env = JsonSerializer.Deserialize<Envelope>(json)
                  ?? throw new InvalidDataException("Invalid license envelope");

        if (env.alg != "AES-GCM-256" || env.sigAlg != "HMAC-SHA256" || env.kdf != "PBKDF2")
            throw new CryptographicException("Unsupported license envelope parameters");

        var master = cfg["License:EncryptionKey"]
                     ?? throw new InvalidOperationException("LICENSE_MASTER_KEY is not set");

        byte[] salt = Convert.FromBase64String(env.salt);
        byte[] iv = Convert.FromBase64String(env.iv);
        byte[] ct = Convert.FromBase64String(env.ciphertext);
        byte[] tag = Convert.FromBase64String(env.tag);
        byte[] sig = Convert.FromBase64String(env.signature);

        // 1) Key derivation (PBKDF2-SHA256, 100000, 32 bytes)
        byte[] keyBytes = PBKDF2(Encoding.UTF8.GetBytes(master), salt, 100_000, 32);

        // 2) Önce HMAC’i doğrula (salt|iv|ciphertext|tag)
        using (var hmac = new HMACSHA256(keyBytes))
        {
            byte[] toSign = Concat(salt, iv, ct, tag);
            byte[] expect = hmac.ComputeHash(toSign);
            if (!FixedTimeEquals(expect, sig))
                throw new CryptographicException("License signature verification failed");
        }

        // 3) AES-GCM decrypt (associatedData: null)
        byte[] plain = new byte[ct.Length];
        using (var aes = new AesGcm(keyBytes))
        {
            aes.Decrypt(iv, ct, tag, plain, associatedData: null);
        }

        var lic = JsonSerializer.Deserialize<PlainLicense>(plain)
                  ?? throw new InvalidDataException("Invalid license payload");

        // Burada lic alanlarını iş kurallarınla validate et (expires, mode, vs.)
        if (lic.expiresAt <= DateTime.UtcNow) throw new CryptographicException("License expired");

        return new LicenseSnapshot()
        {
            Mode = lic.mode,
            Fingerprint = lic.machineFingerprint,
            Features = lic.features ?? new(),
            LoadedAt = DateTime.UtcNow
        };
    }

    static byte[] PBKDF2(byte[] key, byte[] salt, int iter, int len) =>
        new Rfc2898DeriveBytes(key, salt, iter, HashAlgorithmName.SHA256).GetBytes(len);

    static bool FixedTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    static byte[] Concat(params byte[][] parts)
    {
        var len = parts.Sum(p => p.Length);
        var buf = new byte[len];
        int o = 0;
        foreach (var p in parts)
        {
            Buffer.BlockCopy(p, 0, buf, o, p.Length);
            o += p.Length;
        }

        return buf;
    }

    public record PlainLicense(
        string mode,
        string issuer,
        DateTime issuedAt,
        DateTime expiresAt,
        string licenseId,
        string? machineFingerprint,
        List<FeatureSpec>? features);
}