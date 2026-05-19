using MediatR;
using Category.Application.DTOs;

namespace Category.Application.Queries;

public record GetCategoryByIdQuery(int Id) : IRequest<CategoryDto?>;

public record GetCategoryListQuery(
    string? TextFilter = null,
    int PageNumber = 1,
    int PageSize = 10
) : IRequest<PaginatedList<CategoryDto>>;

/// <summary>Returns the full hierarchical tree for the current tenant. When TextFilter is set, bypasses cache and returns only matching subtrees.</summary>
public record GetCategoryTreeQuery(string? TextFilter = null) : IRequest<List<CategoryDto>>;
