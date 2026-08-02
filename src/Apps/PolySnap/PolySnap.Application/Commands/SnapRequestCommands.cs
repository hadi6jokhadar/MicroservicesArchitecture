using MediatR;
using PolySnap.Application.DTOs;

namespace PolySnap.Application.Commands;

public record CreateSnapRequestCommand(
    string Name,
    string RawGeometryGeoJson,
    double Threshold = 0.5
) : IRequest<SnapRequestDto>;

public record UpdateSnapRequestCommand(
    int Id,
    string? Name,
    string? RawGeometryGeoJson,
    string? SnappedGeometryGeoJson,
    double? Threshold
) : IRequest<SnapRequestDto>;

public record DeleteSnapRequestCommand(int Id) : IRequest<bool>;
