namespace ServerGuard.Shared;

/// <summary>
/// Trafik sorgularının sınırları. Sorgu aralığı ve sonuç boyutu sınırsız büyüyemez.
/// </summary>
public static class TrafficQueryConstraints
{
    public const int MinMinutes = 1;
    public const int MaxMinutes = 1440;
    public const int DefaultMinutes = 60;

    public const int MinBucketSeconds = 10;
    public const int MaxBucketSeconds = 3600;
    public const int DefaultBucketSeconds = 60;

    public const int MinTake = 1;
    public const int MaxTake = 100;
    public const int DefaultTake = 10;
}
