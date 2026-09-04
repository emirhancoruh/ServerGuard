namespace ServerGuard.Api.Detection;

/// <summary>
/// IP bazlı kayan pencereleri saklar. Sessizleşen IP'ler otomatik olarak bellekten düşer.
/// </summary>
public interface IFailureWindowStore
{
    /// <summary>
    /// Başarısız denemeyi ilgili sunucu/IP penceresine işler ve eşiğin aşılıp aşılmadığını döner.
    /// </summary>
    bool TryRegisterFailure(string serverName, string sourceIp, DateTimeOffset occurredAt, out int attemptsInWindow);
}
