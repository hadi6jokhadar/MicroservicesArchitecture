using FileManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileManager.Infrastructure.Persistence.Configurations;

public class FileManagerUsageEntityConfiguration : IEntityTypeConfiguration<FileManagerUsageEntity>
{
    public void Configure(EntityTypeBuilder<FileManagerUsageEntity> builder)
    {
        builder.ToTable("FileManagerUsage");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .UseIdentityByDefaultColumn();

        builder.Property(e => e.FileId)
            .IsRequired();

        builder.Property(e => e.UsageArea)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.RowId)
            .IsRequired()
            .HasMaxLength(100);

        // Unique constraint: a file can only be used once per area+row combination
        builder.HasIndex(e => new { e.FileId, e.UsageArea, e.RowId })
            .IsUnique();

        // Index to quickly count usages by file
        builder.HasIndex(e => e.FileId);
    }
}
