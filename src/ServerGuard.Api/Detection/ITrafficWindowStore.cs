namespace ServerGuard.Api.Detection;

/// <summary>
/// IP bazlı istek sayaçlarını saklar ve boşta kalanları temizler.
/// </summary>
public interface ITrafficWindowStore
{
    /// <summary>İzlenen IP sayısı.</summary>
    int TrackedCount { get; }

    /// <summary>
    /// İsteği ilgili sunucu/IP sayacına işler ve eşiğin aşılıp aşılmadığını döner.
    /// </summary>
    bool TryRegisterRequest(string serverName, string clientIp, DateTimeOffset occurredAt, out int requestsInWindow);

    /// <summary>
    /// Belirtilen süredir istek görülmeyen sayaçları siler ve silinen adedi döner.
    /// </summary>
    int RemoveIdle(DateTimeOffset now, TimeSpan idleRetention);
}
