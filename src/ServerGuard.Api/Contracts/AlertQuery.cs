using ServerGuard.Shared;

namespace ServerGuard.Api.Contracts;

/// <summary>
/// <c>GET /api/alerts</c> sorgu parametreleri. Tümü isteğe bağlıdır;
/// sayfa boyutu verilmezse varsayılan kullanılır ve üst sınırla kısıtlanır.
/// </summary>
public sealed record AlertQuery
{
    public string? ServerName { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public int Page { get; init; } = PaginationConstraints.MinPage;

    public int PageSize { get; init; } = PaginationConstraints.DefaultPageSize;
}
