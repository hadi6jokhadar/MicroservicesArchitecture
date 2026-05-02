using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nasheed.Domain.Entities;

namespace Nasheed.Infrastructure.Persistence.Configurations;

public class PlayLogEntityConfiguration : IEntityTypeConfiguration<PlayLogEntity>
{
    public void Configure(EntityTypeBuilder<PlayLogEntity> builder)
    {
        builder.ToTable("PlayLogs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.UserId).IsRequired().HasMaxLength(100);

        builder.HasOne(e => e.Song)
            .WithMany()
            .HasForeignKey(e => e.SongId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
