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
        builder.Property(e => e.FileId).IsRequired();
        builder.Property(e => e.LastError).HasMaxLength(2000);

        builder.HasOne(e => e.Song)
            .WithMany()
            .HasForeignKey(e => e.SongId)
            .OnDelete(DeleteBehavior.Cascade);

        // Prevents duplicate active jobs for the same song + job type (race condition guard).
        // Pending=0, Running=1 — only one active job per (SongId, JobType) at a time.
        builder.HasIndex(e => new { e.SongId, e.JobType })
            .HasFilter("\"JobStatus\" IN (0, 1)")
            .IsUnique()
            .HasDatabaseName("IX_SongIngestionJobs_ActiveJobUnique");
    }
}
