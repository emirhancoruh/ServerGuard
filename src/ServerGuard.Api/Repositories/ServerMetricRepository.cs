using ServerGuard.Api.Data;
using ServerGuard.Api.Data.Entities;

namespace ServerGuard.Api.Repositories;

public sealed class ServerMetricRepository(ServerGuardDbContext dbContext) : IServerMetricRepository
{
    public async Task<ServerMetric> AddAsync(ServerMetric metric, CancellationToken cancellationToken)
    {
        dbContext.ServerMetrics.Add(metric);
        await dbContext.SaveChangesAsync(cancellationToken);
        return metric;
    }
}
