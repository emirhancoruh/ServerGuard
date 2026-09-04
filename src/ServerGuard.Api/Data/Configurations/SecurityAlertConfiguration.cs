using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerGuard.Api.Data.Entities;
using ServerGuard.Shared;

namespace ServerGuard.Api.Data.Configurations;

public sealed class SecurityAlertConfiguration : IEntityTypeConfiguration<SecurityAlert>
{
    public void Configure(EntityTypeBuilder<SecurityAlert> builder)
    {
        builder.HasKey(alert => alert.Id);

        builder.Property(alert => alert.ServerName)
            .HasMaxLength(ServerConstraints.NameMaxLength)
            .IsRequired();

        builder.Property(alert => alert.AlertType)
            .HasConversion<string>()
            .HasMaxLength(AlertConstraints.EnumNameMaxLength)
            .IsRequired();

        builder.Property(alert => alert.Severity)
            .HasConversion<string>()
            .HasMaxLength(AlertConstraints.EnumNameMaxLength)
            .IsRequired();

        builder.Property(alert => alert.SourceIp)
            .HasMaxLength(SecurityEventConstraints.SourceIpMaxLength)
            .IsRequired();

        builder.Property(alert => alert.Description)
            .HasMaxLength(AlertConstraints.DescriptionMaxLength)
            .IsRequired();

        // Panel "en son alarmlar" ve "şu sunucunun alarmları" biçiminde sorgular.
        builder.HasIndex(alert => new { alert.ServerName, alert.Timestamp });
    }
}
