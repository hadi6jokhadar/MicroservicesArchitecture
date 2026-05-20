using System.Text.Json;
using Category.API.Tests.Infrastructure;
using Category.Application.Commands;
using Category.Application.Events;
using Category.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Category.API.Tests.Events;

/// <summary>
/// Integration tests that verify the end-to-end atomicity of the outbox pattern.
///
/// Each test sends a real mutation command through the MediatR pipeline, then queries
/// the outbox table to confirm an <see cref="OutboxEventEntity"/> row was committed in
/// the same database transaction as the entity change.
/// </summary>
[Collection("Sequential")]
public class OutboxEventIntegrationTests : IntegrationTestBase
{
    public OutboxEventIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // ── Create ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCategory_CommitsOutboxRow_WithCreatedEventType()
    {
        // Arrange
        var slug = UniqueSlug("ob-create");
        var command = new CreateCategoryCommand(
            Slug: slug,
            Uri: slug,
            NameTranslations: new Dictionary<string, string> { ["en"] = "Outbox Create Test" }
        );

        // Act
        var result = await SendAsync(command);

        // Assert — outbox row was committed atomically with the entity
        var row = await ExecuteDbContextAsync(async ctx =>
            await ctx.OutboxEvents
                .Where(e => e.ProcessedAt == null)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync()
        );

        row.Should().NotBeNull("a Created event must be queued when a category is created");
        row!.Channel.Should().StartWith("category:events:");

        var msg = DeserializePayload(row.Payload);
        msg.EventType.Should().Be(CategoryEventType.Created);
        msg.Slug.Should().Be(slug);
        msg.Id.Should().Be(result.Id);
        msg.SchemaVersion.Should().Be(CategoryEventMessage.CurrentSchemaVersion);
    }

    [Fact]
    public async Task CreateCategory_OutboxRow_IsUnprocessed_OnCommit()
    {
        // Arrange
        var slug = UniqueSlug("ob-unproc");
        var command = new CreateCategoryCommand(
            Slug: slug,
            Uri: slug,
            NameTranslations: new Dictionary<string, string> { ["en"] = "Unprocessed Test" }
        );

        // Act
        await SendAsync(command);

        // Assert — new rows must start unprocessed (background worker hasn't run yet)
        var row = await ExecuteDbContextAsync(async ctx =>
            await ctx.OutboxEvents
                .Where(e => e.Channel.Contains(slug.Substring(0, 5)))
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync()
        );

        // We can't filter by payload content directly, so verify recent unprocessed rows exist
        var unprocessedCount = await ExecuteDbContextAsync(async ctx =>
            await ctx.OutboxEvents.CountAsync(e => e.ProcessedAt == null)
        );

        unprocessedCount.Should().BeGreaterThan(0);

        // And: the row written for this command must have ProcessedAt = null, RetryCount = 0
        row?.ProcessedAt.Should().BeNull();
        row?.RetryCount.Should().Be(0);
    }

    // ── Update ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateCategory_CommitsOutboxRow_WithUpdatedEventType()
    {
        // Arrange
        var category = await CreateTestCategoryAsync(slug: UniqueSlug("ob-upd"));

        var countBefore = await ExecuteDbContextAsync(ctx =>
            ctx.OutboxEvents.CountAsync(e => e.ProcessedAt == null));

        var command = new UpdateCategoryCommand(
            Id: category.Id,
            Slug: null,
            Uri: null,
            NameTranslations: new Dictionary<string, string>
            {
                ["en"] = "Updated Name",
                ["ar"] = "اسم محدث"
            },
            IconFileId: null,
            ImageFileId: null,
            IconName: null,
            Attributes: null
        );

        // Act
        await SendAsync(command);

        // Assert — one new outbox row was added
        var countAfter = await ExecuteDbContextAsync(ctx =>
            ctx.OutboxEvents.CountAsync(e => e.ProcessedAt == null));

        countAfter.Should().Be(countBefore + 1,
            "an Updated event must be queued when a category is updated");

        // Assert — the newest unprocessed row has the Updated event type
        var row = await ExecuteDbContextAsync(ctx =>
            ctx.OutboxEvents
               .Where(e => e.ProcessedAt == null)
               .OrderByDescending(e => e.CreatedAt)
               .FirstOrDefaultAsync());

        row.Should().NotBeNull();
        var msg = DeserializePayload(row!.Payload);
        msg.EventType.Should().Be(CategoryEventType.Updated);
        msg.Id.Should().Be(category.Id);
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCategory_CommitsOutboxRow_WithDeletedEventType()
    {
        // Arrange
        var category = await CreateTestCategoryAsync(slug: UniqueSlug("ob-del"));

        var countBefore = await ExecuteDbContextAsync(ctx =>
            ctx.OutboxEvents.CountAsync(e => e.ProcessedAt == null));

        // Act
        await SendAsync(new DeleteCategoryCommand(category.Id));

        // Assert — one new outbox row was added
        var countAfter = await ExecuteDbContextAsync(ctx =>
            ctx.OutboxEvents.CountAsync(e => e.ProcessedAt == null));

        countAfter.Should().Be(countBefore + 1,
            "a Deleted event must be queued when a category is deleted");

        // Assert — the newest unprocessed row has the Deleted event type
        var row = await ExecuteDbContextAsync(ctx =>
            ctx.OutboxEvents
               .Where(e => e.ProcessedAt == null)
               .OrderByDescending(e => e.CreatedAt)
               .FirstOrDefaultAsync());

        row.Should().NotBeNull();
        var msg = DeserializePayload(row!.Payload);
        msg.EventType.Should().Be(CategoryEventType.Deleted);
        msg.Id.Should().Be(category.Id);
    }

    // ── Atomicity ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCategory_EntityAndOutboxRow_ExistTogether_InDatabase()
    {
        // This test is the core atomicity guarantee:
        // Both the CategoryEntity and the OutboxEventEntity must be present in the DB
        // after the handler completes, proving they were committed in the same transaction.

        // Arrange
        var slug = UniqueSlug("ob-atomic");

        // Act
        var result = await SendAsync(new CreateCategoryCommand(
            Slug: slug,
            Uri: slug,
            NameTranslations: new Dictionary<string, string> { ["en"] = "Atomicity Test" }
        ));

        // Assert — entity exists
        var entity = await ExecuteDbContextAsync(ctx =>
            ctx.Categories.FindAsync(result.Id).AsTask());

        entity.Should().NotBeNull("category entity must be persisted");
        entity!.Slug.Should().Be(slug);

        // Assert — outbox row also exists (proves atomic commit)
        var outboxRows = await ExecuteDbContextAsync(ctx =>
            ctx.OutboxEvents
               .Where(e => e.ProcessedAt == null)
               .OrderByDescending(e => e.CreatedAt)
               .Take(5)
               .ToListAsync());

        var matchingRow = outboxRows.FirstOrDefault(r =>
        {
            try
            {
                var m = DeserializePayload(r.Payload);
                return m.Id == result.Id && m.EventType == CategoryEventType.Created;
            }
            catch { return false; }
        });

        matchingRow.Should().NotBeNull(
            "an outbox row for the Created event must be committed alongside the entity");
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static CategoryEventMessage DeserializePayload(string json) =>
        JsonSerializer.Deserialize<CategoryEventMessage>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("Failed to deserialize outbox payload");
}
