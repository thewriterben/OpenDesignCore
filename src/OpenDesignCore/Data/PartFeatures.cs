using System.Security.Cryptography;
using System.Text.Json;

namespace OpenDesignCore.Data;

/// <summary>One of the six signed axis directions. Placements and cylinders are restricted to these.</summary>
public enum Axis { PosX, NegX, PosY, NegY, PosZ, NegZ }

public static class AxisExt
{
    public static Axis? OParse(string? str) => str switch
    {
        "+x" => Axis.PosX,
        "-x" => Axis.NegX,
        "+y" => Axis.PosY,
        "-y" => Axis.NegY,
        "+z" => Axis.PosZ,
        "-z" => Axis.NegZ,
        _ => null,
    };

    public static string Str(this Axis e) => e switch
    {
        Axis.PosX => "+x",
        Axis.NegX => "-x",
        Axis.PosY => "+y",
        Axis.NegY => "-y",
        Axis.PosZ => "+z",
        _ => "-z",
    };

    /// <summary>0, 1 or 2 for x, y, z.</summary>
    public static int NIndex(this Axis e) => (int)e / 2;

    /// <summary>+1 or -1.</summary>
    public static int NSign(this Axis e) => (int)e % 2 == 0 ? 1 : -1;
}

/// <summary>
/// Volume a plug, card or finger needs kept free (OpenPartsCore ADR-0007).
/// <see cref="Direction"/> or <see cref="DepthMm"/> null means unknown, and an
/// unknown access is reported, never passed.
/// </summary>
public sealed record FeatureAccess
{
    public Axis? Direction { get; init; }
    public double? DepthMm { get; init; }
    /// <summary>Cross-section perpendicular to the direction, in the order of the remaining axes x, y, z. Null means the item's own face.</summary>
    public double[]? SectionMm { get; init; }
    public required string Source { get; init; }

    public bool BKnown => Direction is not null && DepthMm is not null;
}

/// <summary>An axis-aligned cylinder: <see cref="BaseMm"/> is the centre of the face it starts from.</summary>
public sealed record FeatureCylinder
{
    public required Axis Axis { get; init; }
    public required double[] BaseMm { get; init; }
    public required double LengthMm { get; init; }
    public required double DiameterMm { get; init; }
}

public sealed record PartFeature
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    public double[]? MinMm { get; init; }
    public double[]? MaxMm { get; init; }
    public FeatureCylinder? Cylinder { get; init; }
    public required string Source { get; init; }
    public FeatureAccess? Access { get; init; }

    /// <summary>Axis-aligned bounds of the item in the part frame, whatever its shape.</summary>
    public (double[] Min, double[] Max) OBounds()
    {
        if (Cylinder is null)
            return (MinMm!, MaxMm!);
        FeatureCylinder c = Cylinder;
        double[] aMin = new double[3], aMax = new double[3];
        int nAxis = c.Axis.NIndex();
        double fR = c.DiameterMm / 2.0;
        for (int i = 0; i < 3; i++)
        {
            if (i == nAxis)
            {
                double fEnd = c.BaseMm[i] + c.Axis.NSign() * c.LengthMm;
                aMin[i] = Math.Min(c.BaseMm[i], fEnd);
                aMax[i] = Math.Max(c.BaseMm[i], fEnd);
            }
            else
            {
                aMin[i] = c.BaseMm[i] - fR;
                aMax[i] = c.BaseMm[i] + fR;
            }
        }
        return (aMin, aMax);
    }
}

/// <summary>
/// Where things are on a part, for one hardware revision (OpenPartsCore
/// ADR-0007), as this engine read it. Coordinates are in the part's own
/// frame, millimetres; placing the part is the consumer's job.
/// </summary>
public sealed record PartFeatures
{
    public required string PartId { get; init; }
    public required string Citation { get; init; }
    public required string ModelSha256 { get; init; }
    public required string Revision { get; init; }
    public required string Frame { get; init; }
    /// <summary>What the items do not describe. A clearance check must carry this into its result.</summary>
    public required IReadOnlyList<string> NotModelled { get; init; }
    public required IReadOnlyList<PartFeature> Items { get; init; }
    /// <summary>SHA-256 of the registry file read, and the registry's HEAD, for provenance.</summary>
    public required string FileSha256 { get; init; }
    public required string RegistryCommit { get; init; }
}
