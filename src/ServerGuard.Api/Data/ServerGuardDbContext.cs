using Microsoft.EntityFrameworkCore;
using ServerGuard.Api.Data.Entities;

namespace ServerGuard.Api.Data;

public sealed class ServerGuardDbContext(DbContextOptions<ServerGuardDbContext> options) : DbContext(options)
{
    public DbSet<ServerMetric> ServerMetrics => Set<ServerMetric>();

    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();

    public DbSet<SecurityAlert> SecurityAlerts => Set<SecurityAlert>();

    public DbSet<TrafficLog> TrafficLogs => Set<TrafficLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ServerGuardDbContext).Assembly);
    }
}
