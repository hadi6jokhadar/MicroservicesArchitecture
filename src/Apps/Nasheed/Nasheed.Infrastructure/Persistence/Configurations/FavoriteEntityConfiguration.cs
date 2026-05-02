using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nasheed.Domain.Entities;

namespace Nasheed.Infrastructure.Persistence.Configurations;

public class FavoriteEntityConfiguration : IEntityTypeConfiguration<FavoriteEntity>
{
    public void Configure(EntityTypeBuilder<FavoriteEntity> builder)
    {
        builder.ToTable("Favorites");
        builder.HasKey(e => new { e.UserId, e.SongId });
        builder.Property(e => e.UserId).IsRequired().HasMaxLength(100);

        builder.HasOne(e => e.Song)
            .WithMany()
            .HasForeignKey(e => e.SongId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
