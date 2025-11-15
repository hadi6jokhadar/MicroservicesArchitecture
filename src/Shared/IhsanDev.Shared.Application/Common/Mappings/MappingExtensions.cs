using IhsanDev.Shared.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace IhsanDev.Shared.Application.Common.Mappings;

/// <summary>
/// Manual mapping extensions for pagination and list operations
/// Replaces AutoMapper's ProjectTo functionality
/// </summary>
public static class MappingExtensions
{
    /// <summary>
    /// Creates a paginated list from a queryable
    /// </summary>
    public static Task<PaginatedList<TDestination>> PaginatedListAsync<TDestination>(
        this IQueryable<TDestination> queryable, 
        int pageNumber, 
        int pageSize,
        CancellationToken cancellationToken = default) 
        where TDestination : class
        => PaginatedList<TDestination>.CreateAsync(queryable.AsNoTracking(), pageNumber, pageSize, cancellationToken);

    /// <summary>
    /// Converts a queryable to a list asynchronously with no tracking
    /// </summary>
    public static Task<List<TDestination>> ToListAsync<TDestination>(
        this IQueryable<TDestination> queryable,
        CancellationToken cancellationToken = default) 
        where TDestination : class
        => queryable.AsNoTracking().ToListAsync(cancellationToken);
        
    /// <summary>
    /// Maps a single entity to DTO
    /// </summary>
    public static TDto MapToDto<TSource, TDto>(this TSource source, Func<TSource, TDto> mapper)
        where TDto : class
    {
        return mapper(source);
    }
    
    /// <summary>
    /// Maps a collection of entities to DTOs
    /// </summary>
    public static List<TDto> MapToDto<TSource, TDto>(this IEnumerable<TSource> source, Func<TSource, TDto> mapper)
        where TDto : class
    {
        return source.Select(mapper).ToList();
    }
}