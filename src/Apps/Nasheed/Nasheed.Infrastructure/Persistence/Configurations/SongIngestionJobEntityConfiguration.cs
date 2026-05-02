using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nasheed.Domain.Entities;

namespace Nasheed.Infrastructure.Persistence.Configurations;

public class SongIngestionJobEntityConfiguration : IEntityTypeConfiguration<SongIngestionJobEntity>
{
    public void Configure(EntityTypeBuilder<SongIngestionJobEntity> builder)
    {
        builder.ToTable("SongIngestionJobs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FileId).IsRequired().HasMaxLength(100);
        builder.Property(e => e.LastError).HasMaxLength(2000);

        builder.HasOne(e => e.Song)
            .WithMany()
            .HasForeignKey(e => e.SongId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
