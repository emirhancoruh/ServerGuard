using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Repositories;

public interface IServerRepository
{
    /// <summary>
    /// Verilen andan sonra veri göndermiş sunucuları, en son görülme zamanlarıyla döner.
    /// </summary>
    Task<IReadOnlyList<ServerSummaryDto>> GetKnownServersAsync(DateTimeOffset since, CancellationToken cancellationToken);
}
