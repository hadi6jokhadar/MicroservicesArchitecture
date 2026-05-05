using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nasheed.Domain.Entities;

namespace Nasheed.Infrastructure.Persistence.Configurations;

public class RatingEntityConfiguration : IEntityTypeConfiguration<RatingEntity>
{
    public void Configure(EntityTypeBuilder<RatingEntity> builder)
    {
        builder.ToTable("Ratings");
        builder.HasKey(e => new { e.UserId, e.SongId });
        builder.Property(e => e.UserId).IsRequired();

        builder.HasOne(e => e.Song)
            .WithMany()
            .HasForeignKey(e => e.SongId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
