using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nasheed.Domain.Entities;

namespace Nasheed.Infrastructure.Persistence.Configurations;

public class SongEntityConfiguration : IEntityTypeConfiguration<SongEntity>
{
    public void Configure(EntityTypeBuilder<SongEntity> builder)
    {
        builder.ToTable("Songs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).IsRequired().HasMaxLength(500);
        builder.Property(e => e.FileId).IsRequired();
        builder.Property(e => e.LanguageCode).HasMaxLength(10);
        builder.Property(e => e.VocalStyle).HasMaxLength(100);
        builder.OwnsOne(e => e.LegalCompliance, legal =>
        {
            legal.Property(x => x.CopyrightRiskLevel)
                .HasColumnName("CopyrightRiskLevel")
                .HasMaxLength(10);

            legal.Property(x => x.ContentSafetyFlag)
                .HasColumnName("ContentSafetyFlag")
                .HasMaxLength(10);

            legal.Property(x => x.RiskReason)
                .HasColumnName("RiskReason")
                .HasMaxLength(1000);
        });

        builder.HasOne(e => e.Artist)
            .WithMany()
            .HasForeignKey(e => e.ArtistId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
