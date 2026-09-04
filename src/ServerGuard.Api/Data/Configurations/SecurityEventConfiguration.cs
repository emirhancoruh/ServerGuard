using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerGuard.Api.Data.Entities;
using ServerGuard.Shared;
using ServerGuard.Shared.Enums;

namespace ServerGuard.Api.Data.Configurations;

public sealed class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    /// <summary>Enum adlarının sığacağı uzunluk; sayı yerine metin saklamak sorguları okunur kılar.</summary>
    private const int EventTypeMaxLength = 32;

    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        builder.HasKey(securityEvent => securityEvent.Id);

        builder.Property(securityEvent => securityEvent.ServerName)
            .HasMaxLength(ServerConstraints.NameMaxLength)
            .IsRequired();

        builder.Property(securityEvent => securityEvent.EventType)
            .HasConversion<string>()
            .HasMaxLength(EventTypeMaxLength)
            .IsRequired();

        builder.Property(securityEvent => securityEvent.SourceIp)
            .HasMaxLength(SecurityEventConstraints.SourceIpMaxLength)
            .IsRequired();

        builder.Property(securityEvent => securityEvent.Username)
            .HasMaxLength(SecurityEventConstraints.UsernameMaxLength)
            .IsRequired();

        // Brute-force tespiti "belirli bir IP'nin son X dakikadaki başarısız girişleri" biçiminde sorgulanır.
        builder.HasIndex(securityEvent => new
        {
            securityEvent.ServerName,
            securityEvent.EventType,
            securityEvent.SourceIp,
            securityEvent.Timestamp
        });
    }
}
