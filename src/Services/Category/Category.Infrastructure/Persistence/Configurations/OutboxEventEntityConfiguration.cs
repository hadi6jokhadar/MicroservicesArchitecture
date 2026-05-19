using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Category.Domain.Entities;

namespace Category.Infrastructure.Persistence.Configurations;

public class OutboxEventEntityConfiguration : IEntityTypeConfiguration<OutboxEventEntity>
{
    public void Configure(EntityTypeBuilder<OutboxEventEntity> builder)
    {
        builder.ToTable("category_outbox_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Channel)
            .HasColumnName("channel")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Payload)
            .HasColumnName("payload")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ProcessedAt)
            .HasColumnName("processed_at")
            .IsRequired(false);

        builder.Property(e => e.RetryCount)
            .HasColumnName("retry_count")
            .HasDefaultValue(0);

        builder.Property(e => e.LastError)
            .HasColumnName("last_error")
            .HasMaxLength(2000)
            .IsRequired(false);

        // Fast lookup for the background processor: only unprocessed rows, ordered by creation time
        builder.HasIndex(e => e.ProcessedAt)
            .HasDatabaseName("ix_category_outbox_events_processed_at");

        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("ix_category_outbox_events_created_at");
    }
}
