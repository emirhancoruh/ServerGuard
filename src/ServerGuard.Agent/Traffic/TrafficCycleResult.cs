namespace ServerGuard.Agent.Traffic;

/// <summary>Bir klasör için tek bir okuma turunun sonucu.</summary>
public enum TrafficCycleResult
{
    /// <summary>Tur tamamlandı; okunan her şey teslim edildi ve konum kalıcı hale getirildi.</summary>
    Completed,

    /// <summary>
    /// Backend erişilemedi. Teslim edilmemiş kayıtlar kuyrukta bekliyor; konum ilerlemedi
    /// ve diğer klasörlere geçilmemelidir.
    /// </summary>
    BackendUnavailable
}
