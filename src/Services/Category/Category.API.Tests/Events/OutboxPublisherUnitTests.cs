using System.Text.Json;
using Category.Application.Events;
using Category.Domain.Entities;
using Category.Infrastructure.Persistence;
using Category.Infrastructure.Services;
using IhsanDev.Shared.Kernel.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Category.API.Tests.Events;

/// <summary>
/// Unit tests for <see cref="OutboxCategoryEventPublisher"/>.
///
/// Verifies the core contract of the outbox publisher:
/// - An <see cref="OutboxEventEntity"/> row is queued in the EF change tracker
/// - <c>SaveChangesAsync</c> is NOT called (the caller owns the commit)
/// - The persisted channel and payload contain the expected values
/// </summary>
public class OutboxPublisherUnitTests
{
    // ── helpers ─────────────────────────────────────────────────────────────

    private static CategoryDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CategoryDbContext>()
            .UseInMemoryDatabase($"outbox-unit-{Guid.NewGuid()}")
            .Options;

        return new CategoryDbContext(options);
    }

    private static CategoryEntity BuildEntity(string slug = "test-slug") =>
        CategoryEntity.Create(
            slug: slug,
            uri: slug,
            nameTranslations: LocalizedMapping.From(new Dictionary<string, string>
            {
                ["en"] = "Test Category",
                ["ar"] = "فئة اختبار"
            }),
            parentId: null
        );

    // ── Change-tracker assertions ────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_AddsOneOutboxRow_ToChangeTracker()
    {
        // Arrange
        await using var ctx = CreateInMemoryDbContext();
        var publisher = new OutboxCategoryEventPublisher(ctx, NullLogger<OutboxCategoryEventPublisher>.Instance);
        var entity = BuildEntity();

        // Act
        await publisher.PublishAsync(entity, CategoryEventType.Created, tenantId: null);

        // Assert — row is in the tracker but NOT yet saved
        var added = ctx.ChangeTracker.Entries<OutboxEventEntity>().ToList();
        added.Should().HaveCount(1);
        added[0].State.Should().Be(EntityState.Added);
    }

    [Fact]
    public async Task PublishAsync_DoesNotPersistRow_UntilSaveChangesIsCalled()
    {
        // Arrange
        await using var ctx = CreateInMemoryDbContext();
        var publisher = new OutboxCategoryEventPublisher(ctx, NullLogger<OutboxCategoryEventPublisher>.Instance);
        var entity = BuildEntity();

        // Act — publish but do NOT call SaveChanges
        await publisher.PublishAsync(entity, CategoryEventType.Created, tenantId: null);

        // Assert — database is still empty
        var countInDb = await ctx.OutboxEvents.CountAsync();
        countInDb.Should().Be(0, "PublishAsync must not call SaveChangesAsync");
    }

    [Fact]
    public async Task PublishAsync_AfterSaveChanges_PersistsRow()
    {
        // Arrange
        await using var ctx = CreateInMemoryDbContext();
        var publisher = new OutboxCategoryEventPublisher(ctx, NullLogger<OutboxCategoryEventPublisher>.Instance);
        var entity = BuildEntity();

        // Act — mimic what the handler does: publish, then the repository saves
        await publisher.PublishAsync(entity, CategoryEventType.Created, tenantId: null);
        await ctx.SaveChangesAsync();

        // Assert
        var row = await ctx.OutboxEvents.SingleAsync();
        row.Should().NotBeNull();
        row.ProcessedAt.Should().BeNull("new outbox rows start unprocessed");
        row.RetryCount.Should().Be(0);
    }

    // ── Channel format ───────────────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_WithTenantId_SetsCorrectChannel()
    {
        // Arrange
        await using var ctx = CreateInMemoryDbContext();
        var publisher = new OutboxCategoryEventPublisher(ctx, NullLogger<OutboxCategoryEventPublisher>.Instance);

        // Act
        await publisher.PublishAsync(BuildEntity(), CategoryEventType.Created, tenantId: "tenant-abc");
        await ctx.SaveChangesAsync();

        // Assert
        var row = await ctx.OutboxEvents.SingleAsync();
        row.Channel.Should().Be("category:events:tenant-abc");
    }

    [Fact]
    public async Task PublishAsync_WithNullTenantId_UsesGlobalChannel()
    {
        // Arrange
        await using var ctx = CreateInMemoryDbContext();
        var publisher = new OutboxCategoryEventPublisher(ctx, NullLogger<OutboxCategoryEventPublisher>.Instance);

        // Act
        await publisher.PublishAsync(BuildEntity(), CategoryEventType.Created, tenantId: null);
        await ctx.SaveChangesAsync();

        // Assert
        var row = await ctx.OutboxEvents.SingleAsync();
        row.Channel.Should().Be("category:events:global");
    }

    // ── Payload ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(CategoryEventType.Created)]
    [InlineData(CategoryEventType.Updated)]
    [InlineData(CategoryEventType.Deleted)]
    public async Task PublishAsync_Payload_ContainsCorrectEventType(CategoryEventType eventType)
    {
        // Arrange
        await using var ctx = CreateInMemoryDbContext();
        var publisher = new OutboxCategoryEventPublisher(ctx, NullLogger<OutboxCategoryEventPublisher>.Instance);

        // Act
        await publisher.PublishAsync(BuildEntity(), eventType, tenantId: null);
        await ctx.SaveChangesAsync();

        // Assert
        var row = await ctx.OutboxEvents.SingleAsync();
        var msg = JsonSerializer.Deserialize<CategoryEventMessage>(
            row.Payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        msg.Should().NotBeNull();
        msg!.EventType.Should().Be(eventType);
    }

    [Fact]
    public async Task PublishAsync_Payload_ContainsEntityFields()
    {
        // Arrange
        await using var ctx = CreateInMemoryDbContext();
        var publisher = new OutboxCategoryEventPublisher(ctx, NullLogger<OutboxCategoryEventPublisher>.Instance);
        var entity = BuildEntity(slug: "electronics");

        // Act
        await publisher.PublishAsync(entity, CategoryEventType.Created, tenantId: "t1");
        await ctx.SaveChangesAsync();

        // Assert
        var row = await ctx.OutboxEvents.SingleAsync();
        var msg = JsonSerializer.Deserialize<CategoryEventMessage>(
            row.Payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        msg!.Slug.Should().Be("electronics");
        msg.TenantId.Should().Be("t1");
        msg.SchemaVersion.Should().Be(CategoryEventMessage.CurrentSchemaVersion);
        msg.NameTranslations.Should().ContainKey("en").WhoseValue.Should().Be("Test Category");
    }

    [Fact]
    public async Task PublishAsync_Payload_OccurredAt_IsRecent()
    {
        // Arrange
        await using var ctx = CreateInMemoryDbContext();
        var publisher = new OutboxCategoryEventPublisher(ctx, NullLogger<OutboxCategoryEventPublisher>.Instance);
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Act
        await publisher.PublishAsync(BuildEntity(), CategoryEventType.Updated, tenantId: null);
        await ctx.SaveChangesAsync();

        // Assert
        var row = await ctx.OutboxEvents.SingleAsync();
        var msg = JsonSerializer.Deserialize<CategoryEventMessage>(
            row.Payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        msg!.OccurredAt.Should().BeAfter(before);
        msg.OccurredAt.Should().BeBefore(DateTimeOffset.UtcNow.AddSeconds(5));
    }
}
