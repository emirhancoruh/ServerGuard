namespace ServerGuard.Api.Contracts;

/// <summary><c>GET /api/reports/summary</c> sorgu parametreleri.</summary>
public sealed record ReportSummaryQuery
{
    public string? ServerName { get; init; }

    /// <summary>Verilmezse <c>To</c> değerinden varsayılan aralık kadar geriye gidilir.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Verilmezse şu an kullanılır.</summary>
    public DateTimeOffset? To { get; init; }
}
