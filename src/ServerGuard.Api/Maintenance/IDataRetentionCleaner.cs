namespace ServerGuard.Api.Maintenance;

/// <summary>Saklama süresi dolan kayıtları siler.</summary>
public interface IDataRetentionCleaner
{
    /// <summary>Tüm tabloları temizler ve tablo başına silinen satır sayısını döner.</summary>
    Task<RetentionReport> CleanAsync(CancellationToken cancellationToken);
}
