using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PolySnap.Domain.Entities;

namespace PolySnap.Infrastructure.Persistence.Configurations;

public class SnapRequestEntityConfiguration : IEntityTypeConfiguration<SnapRequestEntity>
{
    public void Configure(EntityTypeBuilder<SnapRequestEntity> builder)
    {
        builder.ToTable("SnapRequests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.RawGeometryGeoJson).IsRequired();
        builder.Property(e => e.SnappedGeometryGeoJson).IsRequired(false);
        builder.Property(e => e.Threshold).HasDefaultValue(0.5);
    }
}
