using System.Security.Cryptography;
using System.Text;

namespace PayFlow.Infrastructure.Services;

public static class WebhookSignatureService
{
    public static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
