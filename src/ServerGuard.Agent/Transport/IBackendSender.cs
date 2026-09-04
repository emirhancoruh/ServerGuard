namespace ServerGuard.Agent.Transport;

/// <summary>
/// Tek bir kaydı backend'e gönderir. Asla exception fırlatmaz; sonucu <see cref="SendResult"/> ile bildirir.
/// </summary>
public interface IBackendSender
{
    Task<SendResult> SendAsync<T>(string route, T payload, CancellationToken cancellationToken);
}
