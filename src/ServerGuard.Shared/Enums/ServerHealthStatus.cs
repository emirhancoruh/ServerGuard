namespace ServerGuard.Shared.Enums;

/// <summary>
/// Sunucunun veri gönderme durumu. Agent susarsa panel bunu fark etmelidir;
/// aksi halde çökmüş bir sunucu, ekranda eski verisiyle sağlıklı görünmeye devam eder.
/// </summary>
public enum ServerHealthStatus
{
    /// <summary>Beklenen aralıkta veri geliyor.</summary>
    Online,

    /// <summary>Veri gecikti; ağ sorunu veya agent yavaşlaması olabilir.</summary>
    Stale,

    /// <summary>Uzun süredir veri yok; sunucu veya agent muhtemelen çalışmıyor.</summary>
    Offline
}
