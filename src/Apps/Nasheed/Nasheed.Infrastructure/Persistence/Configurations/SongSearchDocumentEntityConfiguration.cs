using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nasheed.Domain.Entities;

namespace Nasheed.Infrastructure.Persistence.Configurations;

public class SongSearchDocumentEntityConfiguration : IEntityTypeConfiguration<SongSearchDocumentEntity>
{
    public void Configure(EntityTypeBuilder<SongSearchDocumentEntity> builder)
    {
        builder.ToTable("SongSearchDocuments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EmbeddingJson).IsRequired().HasColumnType("text");
        builder.Property(e => e.EmbeddingModelKey).IsRequired().HasMaxLength(200);
        builder.HasIndex(e => e.SongId).IsUnique();

        builder.HasOne(e => e.Song)
            .WithMany()
            .HasForeignKey(e => e.SongId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
