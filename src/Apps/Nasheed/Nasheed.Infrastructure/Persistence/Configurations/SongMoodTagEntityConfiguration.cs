using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nasheed.Domain.Entities;

namespace Nasheed.Infrastructure.Persistence.Configurations;

public class SongMoodTagEntityConfiguration : IEntityTypeConfiguration<SongMoodTagEntity>
{
    public void Configure(EntityTypeBuilder<SongMoodTagEntity> builder)
    {
        builder.ToTable("SongMoodTags");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Tag).IsRequired().HasMaxLength(100);

        builder.HasOne(e => e.Song)
            .WithMany(s => s.MoodTags)
            .HasForeignKey(e => e.SongId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
