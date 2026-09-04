namespace ServerGuard.Shared.Enums;

public enum AlertType
{
    /// <summary>Aynı kaynaktan kısa sürede çok sayıda başarısız giriş denemesi.</summary>
    BruteForceAttempt,

    /// <summary>Aynı kaynaktan kısa sürede olağan dışı sayıda istek.</summary>
    TrafficAnomaly
}
