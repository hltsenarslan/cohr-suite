using System.Security.Cryptography;
using System.Text;

namespace Core.Api.Helpers;

public static class Sha256Hex
{
    
    public static string Create(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var t in bytes) sb.Append(t.ToString("x2"));
        return sb.ToString();
    }
}