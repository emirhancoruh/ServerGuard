namespace ServerGuard.Shared;

/// <summary>
/// Agent'tan backend'e giden her kaydın ortak alanları. Taşıma katmanının kaydın türünü
/// bilmeden hangi sunucudan ve ne zaman geldiğini loglayabilmesini sağlar.
/// </summary>
public interface IServerPayload
{
    string ServerName { get; }
    DateTimeOffset Timestamp { get; }
}
