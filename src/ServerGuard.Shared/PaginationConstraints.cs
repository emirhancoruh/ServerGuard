namespace ServerGuard.Shared;

/// <summary>
/// Sayfalama sınırları. Sonuç kümesi hiçbir zaman sınırsız büyümez.
/// </summary>
public static class PaginationConstraints
{
    public const int MinPage = 1;
    public const int MinPageSize = 1;
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 25;
}
