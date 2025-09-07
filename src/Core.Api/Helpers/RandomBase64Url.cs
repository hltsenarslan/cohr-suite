using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Core.Api.Helpers;

public static class RandomBase64Url
{
    public static string Create(int bytes = 32)
    {
        var b = RandomNumberGenerator.GetBytes(bytes);
        return Base64UrlEncoder.Encode(b);
    }

}