using ServerGuard.Api.Contracts;
using ServerGuard.Api.Data.Entities;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Repositories;

public interface ISecurityAlertRepository
{
    Task<SecurityAlert> AddAsync(SecurityAlert alert, CancellationToken cancellationToken);

    /// <summary>
    /// Alarmları filtreleyip en yeniden eskiye doğru sayfalayarak döner.
    /// </summary>
    Task<PagedResult<SecurityAlertDto>> QueryAsync(AlertQuery query, CancellationToken cancellationToken);
}
