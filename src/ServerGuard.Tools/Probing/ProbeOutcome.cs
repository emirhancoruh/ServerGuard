namespace ServerGuard.Tools.Probing;

/// <summary>Tek bir kontrolün sonucu.</summary>
public enum ProbeOutcome
{
    /// <summary>Kontrol beklendiği gibi sonuçlandı.</summary>
    Passed,

    /// <summary>Sistem çalışıyor ama dikkat edilmesi gereken bir durum var.</summary>
    Warning,

    /// <summary>Beklenen davranış gerçekleşmedi; müdahale gerekir.</summary>
    Failed,

    /// <summary>Önkoşulu sağlanmadığı için çalıştırılmadı.</summary>
    Skipped
}
