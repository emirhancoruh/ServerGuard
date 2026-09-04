using ServerGuard.Api.Data.Entities;

namespace ServerGuard.Api.Repositories;

public interface ISecurityEventRepository
{
    Task<SecurityEvent> AddAsync(SecurityEvent securityEvent, CancellationToken cancellationToken);
}
