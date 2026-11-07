using System.Globalization;
using System.Numerics;
using OpenDesignCore.Data;
using OpenDesignCore.Models;
using PicoGK;

namespace OpenDesignCore.Fit;

/// <summary>
/// Where a part sits in a fixture: a proper rotation restricted to signed axis
/// permutations, plus a translation. Millimetres (ADR-0004).
///
/// <para>
/// Restricted on purpose. Every fixture this engine has made holds its part
/// square to the fixture's axes, and under a signed permutation an
/// axis-aligned box stays an axis-aligned box — so the check stays exact
/// rather than approximating rotated geometry. A general rotation is a change
/// for when a fixture needs one (ADR-0024).
/// </para>
/// </summary>
public sealed record PartPlacement
{
    /// <summary>Fixture direction the part's +x points along.</summary>
    public required Axis PartXTo { get; init; }
    public required Axis PartYTo { get; init; }
    public required Axis PartZTo { get; init; }
    /// <summary>Where the part frame's origin lands in the fixture frame, mm.</summary>
    public required Vector3 OriginMm { get; init; }
    /// <summary>Why the part sits here, in words, for provenance: which datum meets which.</summary>
    public required string Basis { get; init; }

    private Axis[] AMap => [PartXTo, PartYTo, PartZTo];

    /// <summary>
    /// The placement that puts part point <paramref name="aPartPointMm"/> at
    /// fixture point <paramref name="vecFixturePointMm"/> — which is how a
    /// fixture is designed: this datum on the part meets that face of the
    /// fixture.
    /// </summary>
    public static PartPlacement OLanding(
        Axis ePartXTo, Axis ePartYTo, Axis ePartZTo,
        double[] aPartPointMm, Vector3 vecFixturePointMm, string strBasis)
    {
        PartPlacement oAtZero = new()
        {
            PartXTo = ePartXTo,
            PartYTo = ePartYTo,
            PartZTo = ePartZTo,
            OriginMm = Vector3.Zero,
            Basis = strBasis,
        };
        oAtZero.Validate();
        return oAtZero with { OriginMm = vecFixturePointMm - oAtZero.VecMap(aPartPointMm) };
    }

    /// <summary>Throws unless the three axes are distinct and form a proper (non-mirroring) rotation.</summary>
    public void Validate()
    {
        Axis[] a = AMap;
        if (a.Select(e => e.NIndex()).Distinct().Count() != 3)
            throw new ArgumentException($"Placement maps two part axes onto one fixture axis ({Str()}).");
        // Determinant of a signed permutation matrix: parity of the permutation times the signs.
        int[] n = [.. a.Select(e => e.NIndex())];
        int nInversions = (n[0] > n[1] ? 1 : 0) + (n[0] > n[2] ? 1 : 0) + (n[1] > n[2] ? 1 : 0);
        int nDet = (nInversions % 2 == 0 ? 1 : -1) * a[0].NSign() * a[1].NSign() * a[2].NSign();
        if (nDet != 1)
            throw new ArgumentException(
                $"Placement {Str()} is a mirror image, not a rotation. A physical part cannot be "
                + "placed as its own reflection; one axis sign is wrong.");
    }

    public Axis MapAxis(Axis ePart)
    {
        Axis eTo = AMap[ePart.NIndex()];
        return ePart.NSign() > 0 ? eTo : (Axis)((int)eTo ^ 1);
    }

    public Vector3 VecMap(double[] aP)
    {
        float[] af = [OriginMm.X, OriginMm.Y, OriginMm.Z];
        Axis[] a = AMap;
        for (int i = 0; i < 3; i++)
            af[a[i].NIndex()] += (float)(a[i].NSign() * aP[i]);
        return new Vector3(af[0], af[1], af[2]);
    }

    public (Vector3 Min, Vector3 Max) MapBox(double[] aMin, double[] aMax)
    {
        Vector3 v0 = VecMap(aMin), v1 = VecMap(aMax);
        return (Vector3.Min(v0, v1), Vector3.Max(v0, v1));
    }

    public string Str() =>
        $"part +x->{PartXTo.Str()}, +y->{PartYTo.Str()}, +z->{PartZTo.Str()}, origin at "
        + string.Create(CultureInfo.InvariantCulture, $"({OriginMm.X:F3}, {OriginMm.Y:F3}, {OriginMm.Z:F3}) mm");
}

/// <summary>One place the fixture occupies volume the part needs. Volume in mm³.</summary>
public sealed record AssemblyViolation
{
    public required string FeatureId { get; init; }
    public required string FeatureKind { get; init; }
    /// <summary>"contact": the fixture occupies the item. "access": it occupies the volume a plug, card or finger needs.</summary>
    public required string Kind { get; init; }
    public required double OverlapCubicMm { get; init; }
    public required string Source { get; init; }
}

/// <summary>
/// The outcome of checking one fixture against one part's features. Nothing
/// here is advisory: every list is part of the answer, and
/// <see cref="BPassed"/> is false while any of them says the check could not
/// see something it needed to (ADR-0024).
/// </summary>
public sealed record AssemblyCheckResult
{
    public required string PartId { get; init; }
    public required string Revision { get; init; }
    public required string ModelSha256 { get; init; }
    public required string RegistryFileSha256 { get; init; }
    public required string RegistryCommit { get; init; }
    public required float ToleranceMm { get; init; }
    public required float VoxelSizeMm { get; init; }
    public required string Placement { get; init; }
    public required string PlacementBasis { get; init; }
    public required int ItemsChecked { get; init; }
    public required IReadOnlyList<AssemblyViolation> Violations { get; init; }
    /// <summary>Items whose access volume is unknown and which the caller did NOT accept.</summary>
    public required IReadOnlyList<string> AccessUnverified { get; init; }
    /// <summary>Items whose access volume is unknown and which the caller explicitly accepted, by id.</summary>
    public required IReadOnlyList<string> AccessAccepted { get; init; }
    /// <summary>Items under two voxels on some axis, which cannot be voxelised faithfully: run finer.</summary>
    public required IReadOnlyList<string> TooSmallToCheck { get; init; }
    /// <summary>
    /// Items thinner than twice the tolerance plus two voxels, checked with a
    /// smaller shrink on that axis so they are checked at all. Each says by
    /// how much. A fixture flush against one of these faces can register as
    /// touching it.
    /// </summary>
    public required IReadOnlyList<string> ReducedTolerance { get; init; }
    /// <summary>Carried verbatim from the registry: what the features do not describe.</summary>
    public required IReadOnlyList<string> NotModelled { get; init; }

    public bool BPassed => Violations.Count == 0 && AccessUnverified.Count == 0 && TooSmallToCheck.Count == 0;

    /// <summary>A sidecar block, stable across runs: lists in the order the registry gives them.</summary>
    public Dictionary<string, object?> OProvenanceBlock() => new()
    {
        ["part"] = PartId,
        ["revision"] = Revision,
        ["model_sha256"] = ModelSha256,
        ["registry_file_sha256"] = RegistryFileSha256,
        ["registry_commit"] = RegistryCommit,
        ["tolerance_mm"] = ToleranceMm.ToString("F4", CultureInfo.InvariantCulture),
        ["placement"] = Placement,
        ["placement_basis"] = PlacementBasis,
        ["items_checked"] = ItemsChecked,
        ["passed"] = BPassed,
        ["violations"] = Violations.Select(v => (object?)new Dictionary<string, object?>
        {
            ["feature"] = v.FeatureId,
            ["kind"] = v.Kind,
            ["overlap_mm3"] = v.OverlapCubicMm.ToString("F3", CultureInfo.InvariantCulture),
        }).ToList(),
        ["access_unverified"] = AccessUnverified.Cast<object?>().ToList(),
        ["access_accepted"] = AccessAccepted.Cast<object?>().ToList(),
        ["too_small_to_check"] = TooSmallToCheck.Cast<object?>().ToList(),
        ["reduced_tolerance"] = ReducedTolerance.Cast<object?>().ToList(),
        ["not_modelled"] = NotModelled.Cast<object?>().ToList(),
    };
}

/// <summary>
/// Thrown instead of exporting a fixture the assembly check did not pass. The
/// message names every violation and every gap, because "refused" alone sends
/// someone back to the printer to find out why.
/// </summary>
public sealed class AssemblyCheckFailedException(AssemblyCheckResult oResult) : Exception(StrMessage(oResult))
{
    public AssemblyCheckResult Result { get; } = oResult;

    private static string StrMessage(AssemblyCheckResult r)
    {
        List<string> a =
        [
            $"assembly check failed for {r.PartId} ({r.Revision}); not exporting.",
            $"  placement: {r.Placement}",
        ];
        foreach (AssemblyViolation v in r.Violations)
        {
            a.Add(string.Create(CultureInfo.InvariantCulture,
                $"  {v.Kind,-7} {v.FeatureId,-10} {v.OverlapCubicMm,9:F3} mm³  ({v.FeatureKind})"));
        }
        foreach (string s in r.AccessUnverified)
            a.Add($"  unverified {s}");
        foreach (string s in r.TooSmallToCheck)
            a.Add($"  too small  {s}");
        foreach (string s in r.NotModelled)
            a.Add($"  not modelled: {s}");
        return string.Join(Environment.NewLine, a);
    }
}

/// <summary>
/// Does a fixture leave room for the part it was made for? (ADR-0024)
///
/// <para>
/// Two camera mounts passed every check this repo had and neither could be
/// used, because every check compared the fixture with its own parameters.
/// v0.1 hit the JST connectors; v0.2 walled off the USB-C port. This check
/// compares the fixture with the <i>part</i>: each item in the part's
/// OpenPartsCore <c>features</c> is a keep-out the fixture may not occupy, and
/// each known access volume — the space a plug, card or finger needs — is one
/// too. Both are placed into the fixture's frame, shrunk by the tolerance, and
/// intersected with the fixture's voxels; any remaining volume is a violation.
/// </para>
///
/// <para>
/// <b>Tolerance is a distance and an input.</b> Surfaces closer than it are
/// treated as touching, which is how a slip-fit bore around a barrel passes
/// and a base that the USB body sinks 0.6 mm into does not. It must span at
/// least two voxels, or discretisation alone could produce or hide overlap;
/// below that the check refuses, like a model below its resolution floor.
/// </para>
/// </summary>
public static class AssemblyCheck
{
    public static AssemblyCheckResult Check(
        Library oLib,
        Voxels voxFixture,
        PartFeatures oFeatures,
        PartPlacement oPlacement,
        float fToleranceMm,
        float fVoxelSizeMm,
        IReadOnlyCollection<string>? aAcceptUnverifiedAccess = null)
    {
        oPlacement.Validate();
        if (fToleranceMm < 2 * fVoxelSizeMm)
        {
            throw new ResolutionFloorException(
                $"assembly check: tolerance {fToleranceMm} mm is under two voxels at {fVoxelSizeMm} mm. "
                + "Overlap smaller than the voxel cannot be told from discretisation, so the check "
                + "could report a collision that is not there or miss one that is; refusing.");
        }

        HashSet<string> oAccept = new(aAcceptUnverifiedAccess ?? [], StringComparer.Ordinal);
        HashSet<string> oIds = new(oFeatures.Items.Select(i => i.Id), StringComparer.Ordinal);
        string[] aUnknownAccepts = [.. oAccept.Where(s => !oIds.Contains(s)).Order(StringComparer.Ordinal)];
        if (aUnknownAccepts.Length > 0)
        {
            throw new ArgumentException(
                $"assembly check: accepted-unverified ids {string.Join(", ", aUnknownAccepts)} are not "
                + $"items of {oFeatures.PartId}. An acceptance that names nothing would read as a review.");
        }

        voxFixture.CalculateProperties(out float _, out BBox3 oFixtureBox);

        List<AssemblyViolation> aViolations = [];
        List<string> aUnverified = [], aAccepted = [], aTooSmall = [], aReduced = [];

        foreach (PartFeature oItem in oFeatures.Items)
        {
            // ── the item itself ─────────────────────────────────────────────
            double fContact = FOverlap(oLib, voxFixture, oFixtureBox, oItem, oPlacement,
                fToleranceMm, fVoxelSizeMm, out string? strReduced, out bool bTooSmall);
            if (bTooSmall)
                aTooSmall.Add($"{oItem.Id}: under two voxels ({2 * fVoxelSizeMm} mm) on some axis; run finer");
            else if (fContact > 0)
                aViolations.Add(OViolation(oItem, "contact", fContact, oItem.Source));
            if (strReduced is not null)
                aReduced.Add($"{oItem.Id}: {strReduced}");

            // ── what must stay free around it ───────────────────────────────
            FeatureAccess? oAccess = oItem.Access;
            if (oAccess is null)
                continue;
            if (!oAccess.BKnown)
            {
                string strWhy = $"{oItem.Id}: access "
                    + (oAccess.Direction is null ? "direction and depth" : "depth")
                    + $" unknown ({oAccess.Source})";
                (oAccept.Contains(oItem.Id) ? aAccepted : aUnverified).Add(strWhy);
                continue;
            }
            (double[] aMin, double[] aMax) = OAccessBox(oItem, oAccess);
            double fAccess = FOverlapBox(oLib, voxFixture, oFixtureBox, aMin, aMax, oPlacement,
                fToleranceMm, fVoxelSizeMm, out string? strAccReduced, out bool bAccTooSmall);
            if (bAccTooSmall)
                aTooSmall.Add($"{oItem.Id} access: under two voxels ({2 * fVoxelSizeMm} mm) on some axis; run finer");
            else if (fAccess > 0)
                aViolations.Add(OViolation(oItem, "access", fAccess, oAccess.Source));
            if (strAccReduced is not null)
                aReduced.Add($"{oItem.Id} access: {strAccReduced}");
        }

        return new AssemblyCheckResult
        {
            PartId = oFeatures.PartId,
            Revision = oFeatures.Revision,
            ModelSha256 = oFeatures.ModelSha256,
            RegistryFileSha256 = oFeatures.FileSha256,
            RegistryCommit = oFeatures.RegistryCommit,
            ToleranceMm = fToleranceMm,
            VoxelSizeMm = fVoxelSizeMm,
            Placement = oPlacement.Str(),
            PlacementBasis = oPlacement.Basis,
            ItemsChecked = oFeatures.Items.Count,
            Violations = aViolations,
            AccessUnverified = aUnverified,
            AccessAccepted = aAccepted,
            TooSmallToCheck = aTooSmall,
            ReducedTolerance = aReduced,
            NotModelled = oFeatures.NotModelled,
        };
    }

    private static AssemblyViolation OViolation(PartFeature o, string strKind, double fVol, string strSource) => new()
    {
        FeatureId = o.Id,
        FeatureKind = o.Kind,
        Kind = strKind,
        OverlapCubicMm = fVol,
        Source = strSource,
    };

    /// <summary>
    /// The access volume in the part frame: the item's bounds extruded from
    /// its face on the direction side by the depth, over the item's own face
    /// or the stated section centred on it.
    /// </summary>
    public static (double[] Min, double[] Max) OAccessBox(PartFeature oItem, FeatureAccess oAccess)
    {
        (double[] aItemMin, double[] aItemMax) = oItem.OBounds();
        Axis eDir = oAccess.Direction!.Value;
        double fDepth = oAccess.DepthMm!.Value;
        int d = eDir.NIndex();
        double[] aMin = (double[])aItemMin.Clone(), aMax = (double[])aItemMax.Clone();
        if (eDir.NSign() > 0)
        {
            aMin[d] = aItemMax[d];
            aMax[d] = aItemMax[d] + fDepth;
        }
        else
        {
            aMax[d] = aItemMin[d];
            aMin[d] = aItemMin[d] - fDepth;
        }
        if (oAccess.SectionMm is double[] aSection)
        {
            int k = 0;
            for (int i = 0; i < 3; i++)
            {
                if (i == d)
                    continue;
                double fC = (aItemMin[i] + aItemMax[i]) / 2.0;
                aMin[i] = fC - aSection[k] / 2.0;
                aMax[i] = fC + aSection[k] / 2.0;
                k++;
            }
        }
        return (aMin, aMax);
    }

    private static double FOverlap(
        Library oLib, Voxels voxFixture, BBox3 oFixtureBox, PartFeature oItem,
        PartPlacement oPlacement, float fTol, float fVoxel, out string? strReduced, out bool bTooSmall)
    {
        if (oItem.Cylinder is null)
        {
            return FOverlapBox(oLib, voxFixture, oFixtureBox, oItem.MinMm!, oItem.MaxMm!,
                oPlacement, fTol, fVoxel, out strReduced, out bTooSmall);
        }

        FeatureCylinder c = oItem.Cylinder;
        float fShrinkR = FShrink((float)c.DiameterMm, fTol, fVoxel);
        float fShrinkL = FShrink((float)c.LengthMm, fTol, fVoxel);
        strReduced = null;
        bTooSmall = fShrinkR < 0 || fShrinkL < 0;
        if (bTooSmall)
            return 0;
        if (fShrinkR < fTol || fShrinkL < fTol)
        {
            strReduced = string.Create(CultureInfo.InvariantCulture,
                $"shrunk by {fShrinkR:F3} mm radially and {fShrinkL:F3} mm axially instead of {fTol} mm");
        }
        float fR = (float)(c.DiameterMm / 2.0) - fShrinkR;
        float fLen = (float)c.LengthMm - 2 * fShrinkL;

        Axis eAxis = oPlacement.MapAxis(c.Axis);
        Vector3 vecDir = VecUnit(eAxis);
        Vector3 vecA = oPlacement.VecMap(c.BaseMm) + vecDir * fShrinkL;
        Vector3 vecB = vecA + vecDir * fLen;
        Vector3 vecMin = Vector3.Min(vecA, vecB) - (Vector3.One - VecAbs(vecDir)) * fR;
        Vector3 vecMax = Vector3.Max(vecA, vecB) + (Vector3.One - VecAbs(vecDir)) * fR;
        if (!BIntersects(vecMin, vecMax, oFixtureBox))
            return 0;

        // voxLatticeBeam has hemispherical ends reaching fR past each end;
        // clipping to the exact span makes it the flat-ended cylinder the
        // registry describes. Measured in AssemblyCheckTests.
        Voxels voxBeam = Voxels.voxLatticeBeam(oLib, vecA, fR, vecB, fR);
        Voxels voxSpan = VoxBox(oLib, vecMin, vecMax);
        Voxels voxCyl = voxBeam.voxBoolIntersect(voxSpan);
        return FVolume(voxFixture.voxBoolIntersect(voxCyl));
    }

    /// <summary>
    /// How far to shrink an extent: the tolerance, unless that would leave
    /// under two voxels of it, in which case as much as leaves two. Negative
    /// means the extent itself is under two voxels and cannot be voxelised
    /// faithfully at all.
    /// </summary>
    private static float FShrink(float fExtent, float fTol, float fVoxel)
        => Math.Min(fTol, (fExtent - 2 * fVoxel) / 2f);

    private static double FOverlapBox(
        Library oLib, Voxels voxFixture, BBox3 oFixtureBox, double[] aMin, double[] aMax,
        PartPlacement oPlacement, float fTol, float fVoxel, out string? strReduced, out bool bTooSmall)
    {
        (Vector3 vecMin, Vector3 vecMax) = oPlacement.MapBox(aMin, aMax);
        Vector3 vecSize = vecMax - vecMin;
        Vector3 vecShrink = new(
            FShrink(vecSize.X, fTol, fVoxel), FShrink(vecSize.Y, fTol, fVoxel), FShrink(vecSize.Z, fTol, fVoxel));
        strReduced = null;
        bTooSmall = vecShrink.X < 0 || vecShrink.Y < 0 || vecShrink.Z < 0;
        if (bTooSmall)
            return 0;
        if (vecShrink.X < fTol || vecShrink.Y < fTol || vecShrink.Z < fTol)
        {
            strReduced = string.Create(CultureInfo.InvariantCulture,
                $"shrunk by ({vecShrink.X:F3}, {vecShrink.Y:F3}, {vecShrink.Z:F3}) mm in fixture x, y, z "
                + $"instead of {fTol} mm, to keep two voxels of an extent of ({vecSize.X:F3}, {vecSize.Y:F3}, {vecSize.Z:F3}) mm");
        }
        vecMin += vecShrink;
        vecMax -= vecShrink;
        if (!BIntersects(vecMin, vecMax, oFixtureBox))
            return 0;
        return FVolume(voxFixture.voxBoolIntersect(VoxBox(oLib, vecMin, vecMax)));
    }

    private static Voxels VoxBox(Library oLib, Vector3 vecMin, Vector3 vecMax)
        => new(Utils.mshCreateCube(oLib, vecMax - vecMin, (vecMin + vecMax) / 2f));

    private static double FVolume(Voxels vox)
    {
        if (vox.bIsEmpty())
            return 0;
        vox.CalculateProperties(out float fVolume, out BBox3 _);
        return fVolume;
    }

    /// <summary>
    /// Exact test on axis-aligned bounds: the item lies inside its box and the
    /// fixture inside its own, so disjoint boxes mean zero overlap without
    /// voxelising anything.
    /// </summary>
    private static bool BIntersects(Vector3 vecMin, Vector3 vecMax, BBox3 oBox)
        => vecMin.X < oBox.vecMax.X && vecMax.X > oBox.vecMin.X
        && vecMin.Y < oBox.vecMax.Y && vecMax.Y > oBox.vecMin.Y
        && vecMin.Z < oBox.vecMax.Z && vecMax.Z > oBox.vecMin.Z;

    private static Vector3 VecUnit(Axis e)
    {
        float[] af = [0, 0, 0];
        af[e.NIndex()] = e.NSign();
        return new Vector3(af[0], af[1], af[2]);
    }

    private static Vector3 VecAbs(Vector3 v) => Vector3.Abs(v);
}
