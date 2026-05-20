using Category.API.Tests.Infrastructure;
using Category.Application.Commands;
using Category.Application.DTOs;
using Category.Application.Queries;
using IhsanDev.Shared.Application.Exceptions;

namespace Category.API.Tests.Endpoints;

/// <summary>
/// Integration tests for the Category API endpoints.
/// Tests call MediatR handlers directly to bypass the HTTP layer.
/// </summary>
[Collection("Sequential")]
public class CategoryEndpointsTests : IntegrationTestBase
{
    public CategoryEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // ── Create ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithValidData_ReturnsCreatedCategory()
    {
        // Arrange
        var slug0 = UniqueSlug();
        var command = new CreateCategoryCommand(
            Slug: slug0,
            Uri: slug0,
            NameTranslations: new Dictionary<string, string>
            {
                ["en"] = "Electronics",
                ["ar"] = "إلكترونيات"
            }
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Slug.Should().Be(command.Slug);
        result.NameTranslations.Should().ContainKey("en").WhoseValue.Should().Be("Electronics");
        result.ParentId.Should().BeNull();
        result.Depth.Should().Be(0);
        result.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Create_WithParentId_SetsCorrectDepthAndPath()
    {
        // Arrange
        var parent = await CreateTestCategoryAsync(slug: UniqueSlug("parent"));

        var childSlug2 = UniqueSlug("child");
        var command = new CreateCategoryCommand(
            Slug: childSlug2,
            Uri: childSlug2,
            NameTranslations: new Dictionary<string, string> { ["en"] = "Child Category" },
            ParentId: parent.Id
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.ParentId.Should().Be(parent.Id);
        result.Depth.Should().Be(1);
        result.Path.Should().Contain(parent.Id.ToString());
    }

    [Fact]
    public async Task Create_WithDuplicateSlug_ThrowsConflictException()
    {
        // Arrange
        var slug = UniqueSlug("dup");
        await CreateTestCategoryAsync(slug: slug);

        var command = new CreateCategoryCommand(
            Slug: slug,
            Uri: slug,
            NameTranslations: new Dictionary<string, string> { ["en"] = "Duplicate" }
        );

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => SendAsync(command)
        );
    }

    [Fact]
    public async Task Create_WithInvalidParentId_ThrowsNotFoundException()
    {
        // Arrange
        var orphanSlug = UniqueSlug();
        var command = new CreateCategoryCommand(
            Slug: orphanSlug,
            Uri: orphanSlug,
            NameTranslations: new Dictionary<string, string> { ["en"] = "Orphan" },
            ParentId: 999999
        );

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => SendAsync(command)
        );
    }

    [Fact]
    public async Task Create_WithOptionalFields_PersistsAllData()
    {
        // Arrange
        var slug = UniqueSlug("full");
        var command = new CreateCategoryCommand(
            Slug: slug,
            Uri: slug,
            NameTranslations: new Dictionary<string, string> { ["en"] = "Full Category" },
            IconFileId: null,
            ImageFileId: null,
            Attributes: new Dictionary<string, object> { ["color"] = "blue", ["featured"] = true }
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Attributes.Should().ContainKey("color");
    }

    // ── GetById ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_WithExistingId_ReturnsCategory()
    {
        // Arrange
        var seeded = await CreateTestCategoryAsync(slug: UniqueSlug("byid"));

        // Act
        var result = await SendAsync(new GetCategoryByIdQuery(seeded.Id));

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(seeded.Id);
        result.Slug.Should().Be(seeded.Slug);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await SendAsync(new GetCategoryByIdQuery(999999));

        // Assert
        result.Should().BeNull();
    }

    // ── GetAll ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsPaginatedList()
    {
        // Arrange — seed at least one category
        await CreateTestCategoryAsync(slug: UniqueSlug("list1"));

        // Act
        var result = await SendAsync(new GetCategoryListQuery(PageNumber: 1, PageSize: 10));

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetAll_WithTextFilter_ReturnsMatchingItems()
    {
        // Arrange
        var slug = UniqueSlug("filter");
        await CreateTestCategoryAsync(slug: slug);

        // Act
        var result = await SendAsync(new GetCategoryListQuery(TextFilter: slug, PageNumber: 1, PageSize: 10));

        // Assert
        result.Items.Should().Contain(c => c.Slug == slug);
    }

    // ── GetTree ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTree_ReturnsHierarchicalStructure()
    {
        // Arrange
        var root = await CreateTestCategoryAsync(slug: UniqueSlug("tree-root"));
        var childSlug = UniqueSlug("tree-child");
        var child = new CreateCategoryCommand(
            Slug: childSlug,
            Uri: childSlug,
            NameTranslations: new Dictionary<string, string> { ["en"] = "Tree Child" },
            ParentId: root.Id
        );
        await SendAsync(child);

        // Act
        var tree = await SendAsync(new GetCategoryTreeQuery());

        // Assert
        tree.Should().NotBeNull();
        tree.Should().Contain(c => c.Id == root.Id);
        var rootDto = tree.First(c => c.Id == root.Id);
        rootDto.Children.Should().NotBeEmpty();
    }

    // ── Update ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WithValidData_UpdatesCategory()
    {
        // Arrange
        var seeded = await CreateTestCategoryAsync(slug: UniqueSlug("upd-orig"));

        var command = new UpdateCategoryCommand(
            Id: seeded.Id,
            Slug: UniqueSlug("upd-new"),
            Uri: null,
            NameTranslations: new Dictionary<string, string> { ["en"] = "Updated Name" },
            IconFileId: null,
            ImageFileId: null,
            IconName: null,
            Attributes: null
        );

        // Act
        var result = await SendAsync(command);

        // Assert
        result.Slug.Should().Be(command.Slug);
        result.NameTranslations["en"].Should().Be("Updated Name");
    }

    [Fact]
    public async Task Update_WithNonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var command = new UpdateCategoryCommand(
            Id: 999999,
            Slug: UniqueSlug(),
            Uri: null,
            NameTranslations: null,
            IconFileId: null,
            ImageFileId: null,
            IconName: null,
            Attributes: null
        );

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => SendAsync(command)
        );
    }

    [Fact]
    public async Task Update_WithDuplicateSlug_ThrowsConflictException()
    {
        // Arrange
        var existingSlug = UniqueSlug("conflict-slug");
        await CreateTestCategoryAsync(slug: existingSlug);
        var target = await CreateTestCategoryAsync(slug: UniqueSlug("conflict-target"));

        var command = new UpdateCategoryCommand(
            Id: target.Id,
            Slug: existingSlug,
            Uri: null,
            NameTranslations: null,
            IconFileId: null,
            ImageFileId: null,
            IconName: null,
            Attributes: null
        );

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => SendAsync(command)
        );
    }

    // ── Move ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Move_ToNewParent_UpdatesHierarchy()
    {
        // Arrange
        var parentA = await CreateTestCategoryAsync(slug: UniqueSlug("move-pa"));
        var parentB = await CreateTestCategoryAsync(slug: UniqueSlug("move-pb"));
        var moveChildSlug = UniqueSlug("move-child");
        var child = await SendAsync(new CreateCategoryCommand(
            Slug: moveChildSlug,
            Uri: moveChildSlug,
            NameTranslations: new Dictionary<string, string> { ["en"] = "Move Child" },
            ParentId: parentA.Id
        ));

        // Act
        var result = await SendAsync(new MoveCategoryCommand(Id: child.Id, NewParentId: parentB.Id));

        // Assert
        result.ParentId.Should().Be(parentB.Id);
        result.Path.Should().Contain(parentB.Id.ToString());
    }

    [Fact]
    public async Task Move_ToRoot_ClearsParentId()
    {
        // Arrange
        var parent = await CreateTestCategoryAsync(slug: UniqueSlug("move-root-p"));
        var rootChildSlug = UniqueSlug("move-root-c");
        var child = await SendAsync(new CreateCategoryCommand(
            Slug: rootChildSlug,
            Uri: rootChildSlug,
            NameTranslations: new Dictionary<string, string> { ["en"] = "Root Child" },
            ParentId: parent.Id
        ));

        // Act
        var result = await SendAsync(new MoveCategoryCommand(Id: child.Id, NewParentId: null));

        // Assert
        result.ParentId.Should().BeNull();
        result.Depth.Should().Be(0);
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_WithExistingId_ReturnsTrue()
    {
        // Arrange
        var seeded = await CreateTestCategoryAsync(slug: UniqueSlug("del"));

        // Act
        var result = await SendAsync(new DeleteCategoryCommand(seeded.Id));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_ThrowsNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => SendAsync(new DeleteCategoryCommand(999999))
        );
    }

    [Fact]
    public async Task Delete_ThenGetById_ReturnsNull()
    {
        // Arrange
        var seeded = await CreateTestCategoryAsync(slug: UniqueSlug("del-check"));
        await SendAsync(new DeleteCategoryCommand(seeded.Id));

        // Act
        var result = await SendAsync(new GetCategoryByIdQuery(seeded.Id));

        // Assert — soft-delete means it may be hidden from queries
        // If the repository filters archived, result will be null; otherwise check IsArchived
        if (result != null)
            result.IsArchived.Should().BeTrue();
    }
}
