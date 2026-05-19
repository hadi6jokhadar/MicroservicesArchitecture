using System.Text.Json;
using IhsanDev.Shared.Kernel.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Category.Domain.Entities;

namespace Category.Infrastructure.Persistence.Configurations;

public class CategoryEntityConfiguration : IEntityTypeConfiguration<CategoryEntity>
{
    public void Configure(EntityTypeBuilder<CategoryEntity> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(e => e.Id);

        // ── Hierarchy ────────────────────────────────────────────────────────
        builder.Property(e => e.ParentId)
            .HasColumnName("parent_id")
            .IsRequired(false);

        builder.Property(e => e.Path)
            .HasColumnName("path")
            .IsRequired()
            .HasMaxLength(1000)
            .HasDefaultValue("/");

        builder.Property(e => e.Depth)
            .HasColumnName("depth")
            .IsRequired()
            .HasDefaultValue(0);

        // ── Core Fields ──────────────────────────────────────────────────────
        builder.Property(e => e.Slug)
            .HasColumnName("slug")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Uri)
            .HasColumnName("uri")
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(e => e.IconFileId)
            .HasColumnName("icon_file_id")
            .IsRequired(false);

        builder.Property(e => e.ImageFileId)
            .HasColumnName("image_file_id")
            .IsRequired(false);

        builder.Property(e => e.IconName)
            .HasColumnName("icon_name")
            .HasMaxLength(200)
            .IsRequired(false);

        // ── JSONB: NameTranslations ──────────────────────────────────────────
        // Stores LocalizedMapping as { "en": "Electronics", "ar": "إلكترونيات" }
        builder.Property(e => e.NameTranslations)
            .HasColumnName("name_translations")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValueSql("'{}'::jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v.Translations, JsonOptions),
                v => LocalizedMapping.From(
                    JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonOptions)
                    ?? new Dictionary<string, string>()));

        // ── JSONB: Attributes ────────────────────────────────────────────────
        // Schema-less dynamic attributes stored as jsonb
        builder.Property(e => e.Attributes)
            .HasColumnName("attributes")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValueSql("'{}'::jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, JsonOptions)
                     ?? new Dictionary<string, object>());

        // ── Self-reference navigation ────────────────────────────────────────
        builder.HasOne(e => e.Parent)
            .WithMany(e => e.Children)
            .HasForeignKey(e => e.ParentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Performance Indexes ──────────────────────────────────────────────
        builder.HasIndex(e => e.ParentId)
            .HasDatabaseName("ix_categories_parent_id");

        builder.HasIndex(e => e.Path)
            .HasDatabaseName("ix_categories_path");

        builder.HasIndex(e => e.Slug)
            .IsUnique()
            .HasDatabaseName("ix_categories_slug_unique");

        builder.HasIndex(e => e.Uri)
            .IsUnique()
            .HasDatabaseName("ix_categories_uri_unique");

        builder.HasIndex(e => e.Depth)
            .HasDatabaseName("ix_categories_depth");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
