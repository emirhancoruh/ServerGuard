namespace ServerGuard.Api.Throttling;

/// <summary>Endpoint'lerin bağlandığı hız sınırı politikaları.</summary>
public static class RateLimitPolicies
{
    public const string Ingest = "ingest";
    public const string Panel = "panel";
    public const string Login = "login";
}
