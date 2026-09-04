using ServerGuard.Api.Data;
using ServerGuard.Api.Data.Entities;

namespace ServerGuard.Api.Repositories;

public sealed class SecurityEventRepository(ServerGuardDbContext dbContext) : ISecurityEventRepository
{
    public async Task<SecurityEvent> AddAsync(SecurityEvent securityEvent, CancellationToken cancellationToken)
    {
        dbContext.SecurityEvents.Add(securityEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        return securityEvent;
    }
}
