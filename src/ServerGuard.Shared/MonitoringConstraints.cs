namespace ServerGuard.Shared;

/// <summary>
/// İzleme görünümünün ortak sınırları ve eşikleri.
/// </summary>
public static class MonitoringConstraints
{
    /// <summary>Servis adının türetileceği istek yolu segment sayısı, ör. "/services/kanban".</summary>
    public const int ServiceNameSegmentCount = 2;

    /// <summary>Servis sağlık tablosunda dönülecek en fazla satır.</summary>
    public const int MaxServiceRows = 100;

    /// <summary>Bu oranın üzerindeki 5xx yüzdesi servisi "bozuk" sayar.</summary>
    public const double ErrorRateCriticalPercent = 10;

    /// <summary>Bu oranın üzerindeki 5xx yüzdesi servisi "sorunlu" sayar.</summary>
    public const double ErrorRateWarningPercent = 1;

    /// <summary>Yol bilinmiyorsa kullanılan servis adı.</summary>
    public const string UnknownServiceName = "(bilinmiyor)";
}
