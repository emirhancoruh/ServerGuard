using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerGuard.Api.Data.Entities;
using ServerGuard.Shared;

namespace ServerGuard.Api.Data.Configurations;

public sealed class ServerMetricConfiguration : IEntityTypeConfiguration<ServerMetric>
{
    public void Configure(EntityTypeBuilder<ServerMetric> builder)
    {
        builder.HasKey(metric => metric.Id);

        builder.Property(metric => metric.ServerName)
            .HasMaxLength(ServerConstraints.NameMaxLength)
            .IsRequired();

        builder.HasIndex(metric => new { metric.ServerName, metric.Timestamp });

        // Saklama suresi dolan kayitlari silen temizlik islemi bu sirayi kullanir;
        // indeks olmadan her tur tablonun tamamini tarardi.
        builder.HasIndex(metric => metric.CreatedAt);
    }
}
