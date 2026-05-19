using IhsanDev.Shared.Kernel.Dto;
using IhsanDev.Shared.Kernel.Entities;

namespace Category.Domain.Entities;

/// <summary>
/// Hierarchical category entity supporting tree structures via parent_id / path / depth.
/// </summary>
public class CategoryEntity : BaseEntity
{
    // ── Hierarchy ──────────────────────────────────────────────────────────
    /// <summary>Self-referencing nullable parent. Null means root node.</summary>
    public int? ParentId { get; private set; }

    /// <summary>Materialized path built from Uri segments (e.g. "/electronics/phones/"). Enables fast subtree queries.</summary>
    public string Path { get; private set; } = "/";

    /// <summary>Zero-based depth level. Root = 0.</summary>
    public int Depth { get; private set; }

    // ── Core Fields ────────────────────────────────────────────────────────
    /// <summary>URL-friendly identifier, unique per tenant.</summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>Unique URI string used to build the materialized path.</summary>
    public string Uri { get; private set; } = string.Empty;

    /// <summary>File ID of the icon image stored in File Manager.</summary>
    public int? IconFileId { get; private set; }

    /// <summary>File ID of the banner/cover image stored in File Manager.</summary>
    public int? ImageFileId { get; private set; }

    /// <summary>Display name of the icon (e.g. a CSS class or icon font name).</summary>
    public string? IconName { get; private set; }

    // ── Localization ───────────────────────────────────────────────────────
    /// <summary>Locale → display name mapping stored as JSONB.</summary>
    public LocalizedMapping NameTranslations { get; private set; } = LocalizedMapping.Empty;

    // ── Dynamic Attributes ─────────────────────────────────────────────────
    /// <summary>Schema-less extra attributes stored as JSONB.</summary>
    public Dictionary<string, object> Attributes { get; private set; } = new();

    // ── Navigation ─────────────────────────────────────────────────────────
    public CategoryEntity? Parent { get; private set; }
    public ICollection<CategoryEntity> Children { get; private set; } = new List<CategoryEntity>();

    // EF Core requires a parameterless constructor
    private CategoryEntity() { }

    // ─────────────────────────────────────────────────────────────────────────
    // Factory
    // ─────────────────────────────────────────────────────────────────────────
    public static CategoryEntity Create(
        string slug,
        string uri,
        LocalizedMapping nameTranslations,
        int? parentId = null,
        int? iconFileId = null,
        int? imageFileId = null,
        string? iconName = null,
        Dictionary<string, object>? attributes = null)
    {
        return new CategoryEntity
        {
            Slug = slug,
            Uri = uri,
            NameTranslations = nameTranslations,
            ParentId = parentId,
            IconFileId = iconFileId,
            ImageFileId = imageFileId,
            IconName = iconName,
            Attributes = attributes ?? new Dictionary<string, object>()
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Mutation
    // ─────────────────────────────────────────────────────────────────────────
    public void Update(
        string? slug,
        string? uri,
        LocalizedMapping? nameTranslations,
        int? iconFileId,
        int? imageFileId,
        string? iconName,
        Dictionary<string, object>? attributes)
    {
        if (slug != null) Slug = slug;
        if (uri != null) Uri = uri;
        if (nameTranslations != null) NameTranslations = nameTranslations;
        if (iconFileId.HasValue) IconFileId = iconFileId;
        if (imageFileId.HasValue) ImageFileId = imageFileId;
        if (iconName != null) IconName = iconName;
        if (attributes != null) Attributes = attributes;
    }

    /// <summary>Recalculates Path and Depth based on parent metadata.</summary>
    public void SetHierarchy(int? parentId, string parentPath, int parentDepth)
    {
        ParentId = parentId;
        Depth = parentDepth + 1;
        // Path is built using Uri — call RecalculatePath after setting Uri
    }

    /// <summary>Called after the entity has been persisted. Builds path from Uri segments.</summary>
    public void RecalculatePath(string? parentPath = null)
    {
        if (ParentId == null)
        {
            Path = $"/{Uri}/";
            Depth = 0;
        }
        else
        {
            var baseParentPath = parentPath ?? "/";
            if (!baseParentPath.EndsWith("/")) baseParentPath += "/";
            Path = $"{baseParentPath}{Uri}/";
        }
    }

    /// <summary>Moves this node under a new parent, updating hierarchy metadata.</summary>
    public void MoveTo(int? newParentId, string? newParentPath, int newParentDepth)
    {
        ParentId = newParentId;
        Depth = newParentDepth + 1;
        // Caller must call RecalculatePath and propagate to all descendants
    }
}
