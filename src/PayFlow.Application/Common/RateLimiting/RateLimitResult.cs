namespace PayFlow.Application.Common.RateLimiting;

public sealed record RateLimitResult(
    bool IsAllowed,
    int Limit,
    int Remaining,
    DateTimeOffset ResetAt,
    int? RetryAfterSeconds);
