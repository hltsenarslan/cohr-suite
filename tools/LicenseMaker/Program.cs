// tools/LicenseMaker/Program.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

string Arg(string name, string? def = null)
    => Environment.GetCommandLineArgs().Skip(1)
        .Where(a => a.StartsWith($"--{name}=")).Select(a => a.Substring(name.Length + 3))
        .DefaultIfEmpty(def ?? "").First();

bool Has(string name)
    => Environment.GetCommandLineArgs().Skip(1).Any(a => a == $"--{name}");

var mode = Arg("mode")?.ToLowerInvariant(); // onprem|cloud
if (mode is not ("onprem" or "cloud")) throw new ArgumentException("--mode onprem|cloud");

var expires = DateTime.Parse(Arg("expires") ?? throw new ArgumentException("--expires is required")).ToUniversalTime();
var issuer = Arg("issuer") ?? "CoHR";
var outPath = Arg("out") ?? "license.lic";
var fp = Arg("fingerprint");
var features = new List<FeatureSpec>();

foreach (var a in Environment.GetCommandLineArgs().Skip(1).Where(a => a.StartsWith("--feature=")))
{
    var kv = a.Substring(10);
    var sp = kv.Split('=', 2);
    if (sp.Length == 2 && int.TryParse(sp[1], out var lim))
        features.Add(new FeatureSpec(sp[0].Trim(), lim));
}

if (mode == "onprem")
{
    if (string.IsNullOrWhiteSpace(fp)) throw new ArgumentException("--fingerprint required in onprem");
    if (features.Count == 0) throw new ArgumentException("onprem requires at least one --feature key=limit");
}
else // cloud
{
    fp = null;
    features = null; // Cloud'da dosyada tutulmaz
}

var plain = new PlainLicense(
    mode,
    issuer,
    DateTime.UtcNow,
    expires,
    $"LIC-{DateTime.UtcNow:yyyyMMddHHmmss}",
    fp,
    features
);

var master = Environment.GetEnvironmentVariable("LICENSE_MASTER_KEY")
             ?? throw new InvalidOperationException("LICENSE_MASTER_KEY env required (32+ bytes)");
var salt = RandomNumberGenerator.GetBytes(16);
var iv = RandomNumberGenerator.GetBytes(12);

static byte[] PBKDF2(byte[] key, byte[] salt) =>
    new Rfc2898DeriveBytes(key, salt, 100_000, HashAlgorithmName.SHA256).GetBytes(32);

var keyBytes = PBKDF2(Encoding.UTF8.GetBytes(master), salt);
var plaintext = JsonSerializer.SerializeToUtf8Bytes(plain, new JsonSerializerOptions { WriteIndented = false });

byte[] ciphertext = new byte[plaintext.Length];
byte[] tag = new byte[16];
using (var aes = new AesGcm(keyBytes))
{
    aes.Encrypt(iv, plaintext, ciphertext, tag, associatedData: null);
}

var macKey = new HMACSHA256(keyBytes);
var toSign = salt.Concat(iv).Concat(ciphertext).Concat(tag).ToArray();
var signature = macKey.ComputeHash(toSign);

var env = new Envelope(
    "AES-GCM-256", "HMAC-SHA256", "PBKDF2",
    Convert.ToBase64String(salt),
    Convert.ToBase64String(iv),
    Convert.ToBase64String(ciphertext),
    Convert.ToBase64String(tag),
    Convert.ToBase64String(signature)
);

System.IO.File.WriteAllText(outPath, JsonSerializer.Serialize(env, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"wrote {outPath}");

record FeatureSpec(string key, int userLimit);
record PlainLicense(
    string mode, string issuer, DateTime issuedAt, DateTime expiresAt,
    string licenseId, string? machineFingerprint, List<FeatureSpec>? features);

record Envelope(string alg, string sigAlg, string kdf, string salt, string iv, string ciphertext, string tag, string signature);