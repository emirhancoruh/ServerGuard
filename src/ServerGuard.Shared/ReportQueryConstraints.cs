namespace ServerGuard.Shared;

/// <summary>
/// Rapor sorgularının sınırları.
/// </summary>
/// <remarks>
/// Rapor sorguları toplama (aggregate) yapar ve büyük tablolarda tüm aralığı taramak
/// zorundadır. Aralığı sınırlamak, tek bir isteğin veritabanını uzun süre meşgul edip
/// veri yazan agent'ları bekletmesini engeller.
/// </remarks>
public static class ReportQueryConstraints
{
    /// <summary>Tek bir raporun kapsayabileceği en uzun aralık.</summary>
    public const int MaxRangeDays = 90;

    /// <summary>Tarih verilmediğinde kullanılan varsayılan aralık.</summary>
    public const int DefaultRangeDays = 7;
}
