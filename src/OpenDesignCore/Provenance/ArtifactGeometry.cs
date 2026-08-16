using System.Globalization;
using System.Numerics;
using PicoGK;

namespace OpenDesignCore.Provenance;

/// <summary>
/// The produced artifact's own dimensions, for its provenance record.
///
/// Until this existed the sidecar described only what went *in* — the part
/// envelope, the scan hash, the parameters — and said nothing about how big
/// the thing that came *out* was. A consumer holding the record could not
/// answer "does this fit a 220 mm bed" without re-parsing the STL, which
/// defeats the point of a provenance record travelling with an artifact.
///
/// The two figures are not interchangeable and are deliberately kept apart:
///
/// * <b>Extents</b> come from the mesh's axis-aligned bounding box, at float
///   precision. They never pass through the voxel grid, so voxel size does
///   not bound them. (The scan-compare significance bug was exactly this
///   confusion in the other direction.)
/// * <b>Volume</b> comes from the voxel field and therefore *is* bounded by
///   the voxel size, which the sidecar records alongside.
///
/// Extents are axis-aligned in the artifact's own frame. A consumer deciding
/// whether the part fits a machine may rotate it; that is the consumer's
/// business, and nothing here presumes an orientation.
/// </summary>
public sealed record ArtifactGeometry
{
    public required float BBoxXMm { get; init; }
    public required float BBoxYMm { get; init; }
    public required float BBoxZMm { get; init; }

    /// <summary>Enclosed volume, cubic mm, measured on the voxel field.</summary>
    public required float VolumeCubicMm { get; init; }

    public static ArtifactGeometry OMeasure(Mesh msh, Voxels vox)
    {
        Vector3 vecSize = msh.oBoundingBox().vecSize();
        vox.CalculateProperties(out float fVolume, out BBox3 _);

        return new ArtifactGeometry
        {
            BBoxXMm = vecSize.X,
            BBoxYMm = vecSize.Y,
            BBoxZMm = vecSize.Z,
            VolumeCubicMm = fVolume,
        };
    }

    /// <summary>
    /// The sidecar's whole <c>artifact</c> block: what the artifact is, its
    /// hash, and now its size.
    ///
    /// Lengths are unit-keyed strings at fixed precision, like every other
    /// length in the record — floats are banned from canonical JSON because
    /// their text form is not stable across languages, and this record is
    /// hash-compared against Python.
    /// </summary>
    public Dictionary<string, object?> OArtifactBlock(string strSha256) => new()
    {
        ["media_type"] = "model/stl",
        ["sha256"] = strSha256,
        ["bbox_mm"] = new Dictionary<string, object?>
        {
            ["x"] = StrF2(BBoxXMm),
            ["y"] = StrF2(BBoxYMm),
            ["z"] = StrF2(BBoxZMm),
        },
        ["volume_cubic_mm"] = StrF2(VolumeCubicMm),
    };

    private static string StrF2(double fValue) => fValue.ToString("F2", CultureInfo.InvariantCulture);
}
