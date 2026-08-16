using System.Numerics;
using PicoGK;

namespace OpenDesignCore.Verification;

/// <summary>Per-axis comparison of one dimension, all lengths in mm.</summary>
public sealed record AxisDeviation
{
    public required string Axis { get; init; }
    public required double DesignMm { get; init; }
    public required double ScanMm { get; init; }
    public double DeviationMm => ScanMm - DesignMm;
    /// <summary>
    /// Signed percentage of the design dimension. Negative means the scan is
    /// smaller, which for a print is usually shrinkage.
    /// </summary>
    public double DeviationPct => DesignMm > 0 ? 100.0 * (ScanMm - DesignMm) / DesignMm : 0.0;
}

public sealed record DimensionalReport
{
    public required IReadOnlyList<AxisDeviation> Axes { get; init; }
    public required double DesignVolumeCubicMm { get; init; }
    public required double ScanVolumeCubicMm { get; init; }
    /// <summary>Voxel size used for the volume figures only — see the note on
    /// <see cref="ScanAccuracyMm"/> for why it does not bound the extents.</summary>
    public required double VoxelSizeMm { get; init; }

    /// <summary>
    /// The scanner's stated accuracy in mm, declared by the caller. Zero means
    /// none was declared.
    ///
    /// This replaced an earlier check that compared deviation against the
    /// voxel size, which was simply wrong: extents come from mesh vertices at
    /// float precision and never pass through the voxel grid, so voxel size
    /// bounds the *volume* figures and says nothing about the extents. The
    /// real floor is the scanner's accuracy, which this code cannot know and
    /// therefore will not guess — the same rule as declared units.
    /// </summary>
    public required double ScanAccuracyMm { get; init; }

    /// <summary>
    /// Largest absolute per-axis deviation, mm — the number to compare against
    /// a process tolerance.
    /// </summary>
    public double MaxAbsDeviationMm => Axes.Max(a => Math.Abs(a.DeviationMm));

    /// <summary>
    /// Mean signed per-axis deviation as a percentage. If the axes agree, this
    /// is a candidate isotropic shrinkage compensation value. If they disagree
    /// materially, a single scale factor is the wrong correction and the
    /// spread says so.
    /// </summary>
    public double MeanDeviationPct => Axes.Average(a => a.DeviationPct);

    public double DeviationPctSpread =>
        Axes.Max(a => a.DeviationPct) - Axes.Min(a => a.DeviationPct);

    /// <summary>
    /// Whether the caller declared a scanner accuracy at all. If not, no
    /// statement about significance is possible and none is made.
    /// </summary>
    public bool AccuracyDeclared => ScanAccuracyMm > 0;

    /// <summary>
    /// True when the largest deviation is at or below the declared scanner
    /// accuracy — the measurement cannot tell a real difference from the
    /// instrument. Null when no accuracy was declared: unknown, not false.
    /// </summary>
    public bool? WithinScanAccuracy =>
        AccuracyDeclared ? MaxAbsDeviationMm <= ScanAccuracyMm : null;
}

/// <summary>
/// Compares a printed-and-scanned part against the design that produced it.
///
/// What this measures, precisely: the axis-aligned bounding extents and the
/// enclosed volume of both meshes. That is enough to derive the shrinkage
/// compensation a slicer wants, and it is honest about being a bulk
/// measurement — it is not surface deviation analysis, which needs point-cloud
/// registration (ICP) this deliberately does not attempt.
///
/// Two confounds the caller must not forget, both surfaced in the report:
///   * A scan's absolute scale is only as good as the scanner's calibration.
///     A 0.3% "shrinkage" may be the scanner, not the print.
///   * Extents are orientation-sensitive. A part scanned rotated relative to
///     its design compares different dimensions and the numbers are garbage.
///     Nothing here can detect that, which is why it is written down.
/// </summary>
public static class DimensionalCompare
{
    public static DimensionalReport Compare(
        Mesh mshDesign, Mesh mshScan, float fVoxelSizeMm, float fScanAccuracyMm = 0f)
    {
        if (fScanAccuracyMm < 0)
            throw new ArgumentException("Scan accuracy cannot be negative.");

        BBox3 oDesign = mshDesign.oBoundingBox();
        BBox3 oScan = mshScan.oBoundingBox();
        Vector3 vecDesign = oDesign.vecSize();
        Vector3 vecScan = oScan.vecSize();

        List<AxisDeviation> aAxes =
        [
            new() { Axis = "x", DesignMm = vecDesign.X, ScanMm = vecScan.X },
            new() { Axis = "y", DesignMm = vecDesign.Y, ScanMm = vecScan.Y },
            new() { Axis = "z", DesignMm = vecDesign.Z, ScanMm = vecScan.Z },
        ];

        Voxels voxDesign = new(mshDesign);
        Voxels voxScan = new(mshScan);
        voxDesign.CalculateProperties(out float fDesignVolume, out BBox3 _);
        voxScan.CalculateProperties(out float fScanVolume, out BBox3 _);

        return new DimensionalReport
        {
            Axes = aAxes,
            DesignVolumeCubicMm = fDesignVolume,
            ScanVolumeCubicMm = fScanVolume,
            VoxelSizeMm = fVoxelSizeMm,
            ScanAccuracyMm = fScanAccuracyMm,
        };
    }
}
