using ServerGuard.Api.Data.Entities;

namespace ServerGuard.Api.Repositories;

public interface IServerMetricRepository
{
    Task<ServerMetric> AddAsync(ServerMetric metric, CancellationToken cancellationToken);
}
