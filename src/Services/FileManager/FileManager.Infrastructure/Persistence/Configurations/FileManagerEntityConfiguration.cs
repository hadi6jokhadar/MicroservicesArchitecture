using FileManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileManager.Infrastructure.Persistence.Configurations;

public class FileManagerEntityConfiguration : IEntityTypeConfiguration<FileManagerEntity>
{
    public void Configure(EntityTypeBuilder<FileManagerEntity> builder)
    {
        builder.ToTable("FileManager");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.Extension)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(e => e.Size)
            .IsRequired();

        builder.Property(e => e.Path)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Group)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.Type)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.Temp)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.IsArchived)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.UserId)
            .IsRequired(false);

        builder.Property(e => e.ExternalUrl)
            .IsRequired(false)
            .HasMaxLength(2048);

        builder.Property(e => e.Created)
            .IsRequired();

        builder.Property(e => e.LastModified)
            .IsRequired(false);

        // Indexes for common queries
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.Type);
        builder.HasIndex(e => e.Group);
        builder.HasIndex(e => e.Temp);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.IsArchived);
        builder.HasIndex(e => e.Created);
    }
}
