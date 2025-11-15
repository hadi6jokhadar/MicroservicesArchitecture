namespace IhsanDev.Shared.Application.Common.Mappings;

/// <summary>
/// Marker interface for entities that can be mapped to DTOs
/// No longer uses AutoMapper - manual mapping should be implemented in static MapFrom methods
/// </summary>
public interface IMapFrom<T>
{
    /// <summary>
    /// Creates a DTO from the source entity
    /// </summary>
    static abstract TDto MapFrom<TDto>(T source) where TDto : IMapFrom<T>, new();
}