using MediatR;
using Category.Application.DTOs;
using Category.Application.Events;

namespace Category.Application.Queries;

public record GetCategoryByIdQuery(int Id) : IRequest<CategoryDto?>;

public record GetCategoryListQuery(
    string? TextFilter = null,
    int PageNumber = 1,
    int PageSize = 10
) : IRequest<PaginatedList<CategoryDto>>;

/// <summary>Returns the full hierarchical tree for the current tenant. When TextFilter is set, bypasses cache and returns only matching subtrees.</summary>
public record GetCategoryTreeQuery(string? TextFilter = null) : IRequest<List<CategoryDto>>;

/// <summary>
/// Returns a flat list of all categories serialized as <see cref="CategoryEventMessage"/> records.
/// Used by consumer services on startup to seed their local snapshot table before subscribing
/// to the Redis Pub/Sub channel.
/// </summary>
public record GetCategorySnapshotQuery : IRequest<List<CategoryEventMessage>>;
