using Microsoft.Extensions.Configuration;

namespace PayFlow.Application.Common.RateLimiting;

public sealed record RateLimitPolicy(int Limit, int WindowSeconds)
{
    public static RateLimitPolicy Free(IConfiguration configuration)
    {
        return FromConfiguration(configuration, "FreeTierLimit");
    }

    public static RateLimitPolicy Pro(IConfiguration configuration)
    {
        return FromConfiguration(configuration, "ProTierLimit");
    }

    public static RateLimitPolicy AuthRegister(IConfiguration configuration)
    {
        return FromConfiguration(configuration, "AuthRegisterLimit");
    }

    public static RateLimitPolicy AuthToken(IConfiguration configuration)
    {
        return FromConfiguration(configuration, "AuthTokenLimit");
    }

    private static RateLimitPolicy FromConfiguration(IConfiguration configuration, string limitKey)
    {
        var limit = ParsePositiveInt(configuration, $"RateLimiting:{limitKey}");
        var windowSeconds = ParsePositiveInt(configuration, "RateLimiting:WindowSeconds");

        return new RateLimitPolicy(limit, windowSeconds);
    }

    private static int ParsePositiveInt(IConfiguration configuration, string key)
    {
        if (!int.TryParse(configuration[key], out var value) || value <= 0)
        {
            throw new InvalidOperationException($"{key} must be greater than zero.");
        }

        return value;
    }
}
