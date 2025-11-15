namespace IhsanDev.Shared.Application.Common.Mappings;

/// <summary>
/// Manual mapping helper class
/// Replaces AutoMapper's Profile functionality with simple delegate-based mapping
/// </summary>
public static class ManualMapper
{
    /// <summary>
    /// Maps a single entity to a DTO using a mapping function
    /// </summary>
    public static TDto Map<TSource, TDto>(TSource source, Func<TSource, TDto> mappingFunc)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        
        return mappingFunc(source);
    }
    
    /// <summary>
    /// Maps a collection of entities to DTOs using a mapping function
    /// </summary>
    public static List<TDto> MapList<TSource, TDto>(IEnumerable<TSource> source, Func<TSource, TDto> mappingFunc)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        
        return source.Select(mappingFunc).ToList();
    }
}