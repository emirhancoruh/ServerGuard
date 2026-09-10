using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServerGuard.Api.Data;
using ServerGuard.Api.Data.Entities;

namespace ServerGuard.Api.Maintenance;

/// <summary>
/// Saklama süresi dolan kayıtları küçük partiler hâlinde siler.
/// </summary>
/// <remarks>
/// Silme, kimliklerden oluşan bir alt sorgu üzerinden yapılır; böylece tek bir DELETE
/// ifadesi sabit sayıda satıra dokunur, tablo uzun süre kilitlenmez ve iptal isteği
/// partiler arasında etkili olur.
/// </remarks>
public sealed class DataRetentionCleaner(
    ServerGuardDbContext dbContext,
    IOptions<RetentionOptions> options,
    TimeProvider timeProvider,
    ILogger<DataRetentionCleaner> logger) : IDataRetentionCleaner
{
    private readonly RetentionOptions _options = options.Value;

    public async Task<RetentionReport> CleanAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        return new RetentionReport(
            await PurgeAsync<ServerMetric>(now - _options.ServerMetrics, cancellationToken),
            await PurgeAsync<TrafficLog>(now - _options.TrafficLogs, cancellationToken),
            await PurgeAsync<SecurityEvent>(now - _options.SecurityEvents, cancellationToken),
            await PurgeAsync<SecurityAlert>(now - _options.SecurityAlerts, cancellationToken));
    }

    private async Task<int> PurgeAsync<TEntity>(DateTimeOffset cutoff, CancellationToken cancellationToken)
        where TEntity : class, IRetainedRecord
    {
        var deletedTotal = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var expiredIds = dbContext.Set<TEntity>()
                .Where(record => record.CreatedAt < cutoff)
                .OrderBy(record => record.Id)
                .Select(record => record.Id)
                .Take(_options.BatchSize);

            var deleted = await dbContext.Set<TEntity>()
                .Where(record => expiredIds.Contains(record.Id))
                .ExecuteDeleteAsync(cancellationToken);

            deletedTotal += deleted;

            // Parti dolmadıysa silinecek kayıt kalmamıştır.
            if (deleted < _options.BatchSize)
            {
                break;
            }
        }

        if (deletedTotal > 0)
        {
            logger.LogInformation(
                "Retention purge removed {DeletedCount} row(s) from {Entity} older than {Cutoff:u}.",
                deletedTotal,
                typeof(TEntity).Name,
                cutoff);
        }

        return deletedTotal;
    }
}
