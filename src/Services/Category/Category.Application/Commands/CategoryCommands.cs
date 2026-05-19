using MediatR;
using Category.Application.DTOs;

namespace Category.Application.Commands;

/// <summary>Creates a new category node, optionally under a parent.</summary>
public record CreateCategoryCommand(
    string Slug,
    string Uri,
    Dictionary<string, string> NameTranslations,
    int? ParentId = null,
    int? IconFileId = null,
    int? ImageFileId = null,
    string? IconName = null,
    Dictionary<string, object>? Attributes = null
) : IRequest<CategoryDto>;

/// <summary>Updates an existing category's fields (all nullable = partial update).</summary>
public record UpdateCategoryCommand(
    int Id,
    string? Slug,
    string? Uri,
    Dictionary<string, string>? NameTranslations,
    int? IconFileId,
    int? ImageFileId,
    string? IconName,
    Dictionary<string, object>? Attributes
) : IRequest<CategoryDto>;

/// <summary>Moves a category node to a new parent (or to root when NewParentId is null).</summary>
public record MoveCategoryCommand(
    int Id,
    int? NewParentId
) : IRequest<CategoryDto>;

/// <summary>Soft-deletes a category (archives it).</summary>
public record DeleteCategoryCommand(int Id) : IRequest<bool>;
