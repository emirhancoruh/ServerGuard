using Microsoft.EntityFrameworkCore;
using ServerGuard.Api.Contracts;
using ServerGuard.Api.Data;
using ServerGuard.Api.Data.Entities;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Repositories;

public sealed class SecurityAlertRepository(ServerGuardDbContext dbContext) : ISecurityAlertRepository
{
    public async Task<SecurityAlert> AddAsync(SecurityAlert alert, CancellationToken cancellationToken)
    {
        dbContext.SecurityAlerts.Add(alert);
        await dbContext.SaveChangesAsync(cancellationToken);
        return alert;
    }

    public async Task<PagedResult<SecurityAlertDto>> QueryAsync(AlertQuery query, CancellationToken cancellationToken)
    {
        var filtered = ApplyFilters(dbContext.SecurityAlerts.AsNoTracking(), query);

        var totalCount = await filtered.CountAsync(cancellationToken);

        var items = await filtered
            // Zaman damgaları eşit olduğunda sıralamanın kararlı kalması için Id ikincil anahtardır.
            .OrderByDescending(alert => alert.Timestamp)
            .ThenByDescending(alert => alert.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(alert => new SecurityAlertDto(
                alert.Id,
                alert.ServerName,
                alert.AlertType,
                alert.Severity,
                alert.SourceIp,
                alert.ObservedCount,
                alert.Description,
                alert.Timestamp,
                alert.AbuseConfidenceScore))
            .ToListAsync(cancellationToken);

        return new PagedResult<SecurityAlertDto>(items, query.Page, query.PageSize, totalCount);
    }

    private static IQueryable<SecurityAlert> ApplyFilters(IQueryable<SecurityAlert> source, AlertQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.ServerName))
        {
            source = source.Where(alert => alert.ServerName == query.ServerName);
        }

        if (query.From.HasValue)
        {
            source = source.Where(alert => alert.Timestamp >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            source = source.Where(alert => alert.Timestamp <= query.To.Value);
        }

        return source;
    }
}
