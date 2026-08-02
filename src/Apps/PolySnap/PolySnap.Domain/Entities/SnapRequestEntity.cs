using IhsanDev.Shared.Kernel.Entities;

namespace PolySnap.Domain.Entities;

/// <summary>
/// A user-submitted freehand map shape awaiting (or having completed) the spatial
/// snapping process. RawGeometryGeoJson is the rough input; SnappedGeometryGeoJson is
/// the computed precise polygon result, populated once processing completes.
///
/// NOTE: This is a CRUD-only scaffold. The actual PostGIS/OSM snapping logic that
/// populates SnappedGeometryGeoJson is out of scope for this task and will be added
/// in a later phase.
/// </summary>
public class SnapRequestEntity : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string RawGeometryGeoJson { get; private set; } = string.Empty;
    public string? SnappedGeometryGeoJson { get; private set; }
    public double Threshold { get; private set; } = 0.5;

    private SnapRequestEntity() { }

    public static SnapRequestEntity Create(
        string name,
        string rawGeometryGeoJson,
        double threshold = 0.5,
        string? snappedGeometryGeoJson = null)
    {
        return new SnapRequestEntity
        {
            Name = name,
            RawGeometryGeoJson = rawGeometryGeoJson,
            Threshold = threshold,
            SnappedGeometryGeoJson = snappedGeometryGeoJson
        };
    }

    public void Update(
        string? name,
        string? rawGeometryGeoJson,
        string? snappedGeometryGeoJson,
        double? threshold)
    {
        if (name != null) Name = name;
        if (rawGeometryGeoJson != null) RawGeometryGeoJson = rawGeometryGeoJson;
        if (snappedGeometryGeoJson != null) SnappedGeometryGeoJson = snappedGeometryGeoJson;
        if (threshold.HasValue) Threshold = threshold.Value;
    }
}
