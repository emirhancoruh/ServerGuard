using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerGuard.Api.Data.Entities;
using ServerGuard.Shared;

namespace ServerGuard.Api.Data.Configurations;

public sealed class TrafficLogConfiguration : IEntityTypeConfiguration<TrafficLog>
{
    public void Configure(EntityTypeBuilder<TrafficLog> builder)
    {
        builder.HasKey(log => log.Id);

        builder.Property(log => log.ServerName)
            .HasMaxLength(ServerConstraints.NameMaxLength)
            .IsRequired();

        builder.Property(log => log.ClientIp)
            .HasMaxLength(TrafficConstraints.ClientIpMaxLength)
            .IsRequired();

        builder.Property(log => log.RequestPath)
            .HasMaxLength(TrafficConstraints.RequestPathMaxLength)
            .IsRequired();

        // "Şu sunucunun son X dakikadaki trafiği" sorgusu için.
        builder.HasIndex(log => new { log.ServerName, log.Timestamp });

        // "Şu IP'nin son X dakikadaki istekleri" — anormal patern analizinin ihtiyaç duyduğu sıra.
        builder.HasIndex(log => new { log.ClientIp, log.Timestamp });

        // Saklama suresi dolan kayitlari silen temizlik islemi bu sirayi kullanir;
        // indeks olmadan her tur tablonun tamamini tarardi.
        builder.HasIndex(log => log.CreatedAt);
    }
}
