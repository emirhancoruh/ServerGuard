namespace ServerGuard.Api.Reputation;

/// <summary>AbuseIPDB <c>/check</c> yanıtının ihtiyaç duyulan kısmı.</summary>
public sealed record AbuseIpDbCheckResponse(AbuseIpDbCheckData? Data);

public sealed record AbuseIpDbCheckData(int AbuseConfidenceScore, int TotalReports, string? CountryCode);
