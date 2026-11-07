using System.Numerics;
using OpenDesignCore.Data;
using OpenDesignCore.Fit;
using OpenDesignCore.Models;
using OpenDesignCore.Tests.Reference.Historical;
using PicoGK;
using Xunit;
using Xunit.Abstractions;

namespace OpenDesignCore.Tests.Reference;

/// <summary>
/// Reference case: the assembly check (ADR-0024) must reject the two camera
/// mounts that were printed and could not be used — for the reasons they
/// actually failed on the bench.
///
/// <para>
/// <b>Source:</b> the bench. camera-mount/0.1 (a56a7ac) would not accept the
/// board: its slots met the JST connectors at the bottom corners (reported
/// 2026-09-25). camera-mount/0.2 (385feef, bore from 28796e7) held the board by
/// its lens but the USB-C port could not be plugged in: it points into the
/// base (reported 2026-09-26). The part geometry is OpenPartsCore's
/// <c>features</c> for <c>boards/lilygo-t-camera-plus-s3</c> at V1.0-V1.1, read
/// from the vendor's STEP.
/// </para>
///
/// <para>
/// <b>What is not known, and how the test copes.</b> The lens axis's position
/// across the board is not yet measured, so every assertion must hold at the
/// board centre and ±2 mm either side. For the historical mounts both lens
/// faces are swept as well, as they were before the face was confirmed
/// (2026-09-26); for v0.3 the confirmed face is used. A conclusion that
/// depended on an unmeasured value would fail at least one case.
/// </para>
///
/// <para>
/// <b>And the positive control is v0.3</b>, the same plate and bore with the
/// board hung USB edge up. The check must find nothing there, including for a
/// plug longer than the fixture is tall. A check that cannot say yes proves
/// nothing by saying no.
/// </para>
///
/// <para>
/// <b>If it starts failing:</b> either the registry's features changed (look
/// at the revision and model hash in the output) or the check stopped seeing
/// geometry it used to — and a check that no longer rejects these two parts
/// would pass the next one that fails the same way.
/// </para>
/// </summary>
public sealed class RetroactiveFixtureCheckTest(ITestOutputHelper oOutput)
{
    private const string PartId = "boards/lilygo-t-camera-plus-s3";
    private const float Voxel = 0.15f;       // at or under both models' resolution floors
    private const float Tol = 2 * Voxel;     // the smallest tolerance the check will accept

    private static PartFeatures? OFeaturesOrNull()
    {
        string? strDir = TestRegistry.StrSiblingRegistryOrNull();
        if (strDir is null)
            return null;
        List<string> aErrors = [];
        PartFeatures? o = PartsRegistry.OLoadFeatures(strDir, PartId, aErrors);
        Assert.True(o is not null,
            "OpenPartsCore is checked out but gives no usable features for the camera board. "
            + "These tests need OpenPartsCore ADR-0008's data (on its main since PR #18): "
            + string.Join("; ", aErrors));
        return o;
    }

    /// <summary>
    /// The laminate in the part frame, from the features' own Board item, so
    /// the placement rests on the registry rather than on a number typed here.
    /// </summary>
    private static (double[] Min, double[] Max) OLaminate(PartFeatures o)
        => o.Items.Single(i => i.Id == "Board").OBounds();

    private void Report(string strCase, AssemblyCheckResult r)
    {
        oOutput.WriteLine($"{strCase}: {r.Placement}");
        oOutput.WriteLine($"  revision {r.Revision}, model {r.ModelSha256[..12]}, registry {r.RegistryCommit[..Math.Min(12, r.RegistryCommit.Length)]}");
        foreach (AssemblyViolation v in r.Violations)
            oOutput.WriteLine($"  {v.Kind,-7} {v.FeatureId,-10} {v.OverlapCubicMm,10:F3} mm³");
        oOutput.WriteLine(
            $"  access unverified: {r.AccessUnverified.Count}; reduced tolerance: {r.ReducedTolerance.Count}; "
            + $"too small at {Voxel} mm: {r.TooSmallToCheck.Count}");
    }

    // ── v0.1: board in two edge slots, bottom edge on the slot floor ─────────

    public static TheoryData<bool> Faces => new() { true, false };

    [Theory]
    [MemberData(nameof(Faces))]
    public void CameraMountV01IsRejectedForTheJstConnectors(bool bLensOnLowFace)
    {
        PartFeatures? oF = OFeaturesOrNull();
        if (oF is null)
            return;
        (double[] aLo, double[] aHi) = OLaminate(oF);

        // v0.1's frame: X centred on the board, Y = 0 on the PCB mid-plane,
        // lens toward -Y, bottom edge on the seat. The laminate's centre on
        // its bottom edge lands at (0, 0, seat).
        PartPlacement oPlace = PartPlacement.OLanding(
            bLensOnLowFace ? Axis.NegX : Axis.PosX,
            Axis.PosZ,
            bLensOnLowFace ? Axis.PosY : Axis.NegY,
            [(aLo[0] + aHi[0]) / 2, aLo[1], (aLo[2] + aHi[2]) / 2],
            new Vector3(0, 0, CameraMountV01.SeatZMm),
            "v0.1: laminate centred between the slots, mid-plane on Y = 0, bottom edge on the slot floor");

        using OdcLibrary oLib = new(Voxel);
        Voxels vox = CameraMountV01.VoxBuild(oLib, Voxel);
        AssemblyCheckResult r = AssemblyCheck.Check(oLib, vox, oF, oPlace, Tol, Voxel);
        Report($"v0.1, lens on {(bLensOnLowFace ? "z=0" : "z=top")} face", r);

        Assert.False(r.BPassed);
        Assert.Contains(r.Violations, v => v is { FeatureId: "CN1", Kind: "contact" });
        Assert.Contains(r.Violations, v => v is { FeatureId: "CN2", Kind: "contact" });
    }


    // ── v0.2: board hung on its lens barrel, USB edge down ───────────────────

    public static TheoryData<bool, double> FacesAndOffsets
    {
        get
        {
            TheoryData<bool, double> o = [];
            foreach (bool b in new[] { true, false })
                foreach (double f in new[] { -2.0, 0.0, 2.0 })
                    o.Add(b, f);
            return o;
        }
    }

    /// <summary>The registry's features with one item's access depth replaced, for a probe.</summary>
    private static PartFeatures OWithDepth(PartFeatures oF, string strId, double fDepthMm, string strWhy) => oF with
    {
        Items = [.. oF.Items.Select(i => i.Id != strId ? i : i with
        {
            Access = i.Access! with { DepthMm = fDepthMm, Source = strWhy },
        })],
    };

    [Theory]
    [MemberData(nameof(FacesAndOffsets))]
    public void CameraMountV02IsRejectedBecauseTheUsbPortSitsInTheBase(bool bLensOnLowFace, double fOffsetMm)
    {
        PartFeatures? oF = OFeaturesOrNull();
        if (oF is null)
            return;
        using OdcLibrary oLib = new(Voxel);
        Voxels vox = CameraMountV02.VoxBuild(oLib, Voxel);
        AssemblyCheckResult r = AssemblyCheck.Check(
            oLib, vox, oF, CameraMountV02.OPlacement(oF, bLensOnLowFace, fOffsetMm), Tol, Voxel);
        Report($"v0.2, lens on {(bLensOnLowFace ? "z=0" : "z=top")} face, offset {fOffsetMm:+0.0;-0.0;0}", r);

        // The receptacle body overhangs the board edge by 1.6 mm and the base
        // is only 1.0 mm below that edge: the body is inside the base.
        Assert.False(r.BPassed);
        Assert.Contains(r.Violations, v => v is { FeatureId: "USB1", Kind: "contact" });
        // The registry does not yet know how long a plug is, so it must say so
        // rather than report the port as reachable.
        Assert.Contains(r.AccessUnverified, s => s.StartsWith("USB1:", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(FacesAndOffsets))]
    public void AnyPlugAtAllWouldHitV02sBase(bool bLensOnLowFace, double fOffsetMm)
    {
        // The access half, without inventing a plug length. The shortest plug
        // body the check can resolve is one voxel beyond the tolerance band on
        // each side; if even that collides, every real USB-C plug does too,
        // since every real one is longer. The depth is derived from the check's
        // own resolution, not claimed as a property of any cable.
        PartFeatures? oF = OFeaturesOrNull();
        if (oF is null)
            return;
        float fShortest = 2 * Tol + Voxel;
        PartFeatures oProbe = OWithDepth(oF, "USB1", fShortest, "probe: shortest resolvable depth");
        using OdcLibrary oLib = new(Voxel);
        Voxels vox = CameraMountV02.VoxBuild(oLib, Voxel);
        AssemblyCheckResult r = AssemblyCheck.Check(
            oLib, vox, oProbe, CameraMountV02.OPlacement(oF, bLensOnLowFace, fOffsetMm), Tol, Voxel);
        Report($"v0.2 probe {fShortest:F2} mm plug, lens on {(bLensOnLowFace ? "z=0" : "z=top")}, offset {fOffsetMm}", r);

        Assert.Contains(r.Violations, v => v is { FeatureId: "USB1", Kind: "access" });
    }

    // ── v0.3: the same hold, USB edge up — the positive control ──────────────

    public static TheoryData<double> Offsets => new() { -2.0, 0.0, 2.0 };

    /// <summary>
    /// The shipping model's placement (CameraMountModel.OPlacement), with the
    /// one value still unmeasured — the axis's offset across the board — set
    /// to a hypothesis. The lens face is confirmed (2026-09-26) and not swept.
    /// </summary>
    private static PartPlacement OV03Placement(PartFeatures oF, CameraMountParams p, double fLensOffsetXMm)
    {
        (double[] aLo, double[] aHi) = OLaminate(oF);
        return CameraMountModel.OPlacement(p with
        {
            LensAxisFromLowXEdgeMm = (float)((aHi[0] - aLo[0]) / 2 + fLensOffsetXMm),
        }, oF);
    }

    [Theory]
    [MemberData(nameof(Offsets))]
    public void CameraMountV03TouchesNothingOnTheBoard(double fOffsetMm)
    {
        // The check must be able to say yes. If it found contact here, where
        // the board hangs in open air on its barrel, it would be reporting
        // geometry that is not there, and its rejections of v0.1 and v0.2
        // would mean nothing.
        PartFeatures? oF = OFeaturesOrNull();
        if (oF is null)
            return;
        CameraMountParams p = CameraMountParams.ODefault();
        using OdcLibrary oLib = new(Voxel);
        Voxels vox = CameraMountModel.VoxBuild(oLib, p, Voxel);
        AssemblyCheckResult r = AssemblyCheck.Check(oLib, vox, oF, OV03Placement(oF, p, fOffsetMm), Tol, Voxel);
        Report($"v0.3, offset {fOffsetMm:+0.0;-0.0;0}", r);

        Assert.Empty(r.Violations);
        Assert.Empty(r.TooSmallToCheck);
        Assert.NotEmpty(r.NotModelled);
    }

    [Theory]
    [MemberData(nameof(Offsets))]
    public void EveryPlugLengthReachesV03sPorts(double fOffsetMm)
    {
        // The reverse of the v0.2 probe. Give USB1, CN1 and CN2 an access
        // depth taller than the whole fixture: if nothing blocks that, nothing
        // blocks any real plug, whatever its length turns out to be when
        // measured. The depth is the fixture's own height, not a cable figure.
        PartFeatures? oF = OFeaturesOrNull();
        if (oF is null)
            return;
        CameraMountParams p = CameraMountParams.ODefault();
        double fTaller = CameraMountModel.FOuterZMm(p) + CameraMountModel.FOuterYMm(p);
        PartFeatures oProbe = oF;
        foreach (string strId in new[] { "USB1", "CN1", "CN2" })
            oProbe = OWithDepth(oProbe, strId, fTaller, "probe: deeper than the fixture is tall");

        using OdcLibrary oLib = new(Voxel);
        Voxels vox = CameraMountModel.VoxBuild(oLib, p, Voxel);
        AssemblyCheckResult r = AssemblyCheck.Check(oLib, vox, oProbe, OV03Placement(oF, p, fOffsetMm), Tol, Voxel);
        Report($"v0.3 probe {fTaller:F0} mm plugs, offset {fOffsetMm:+0.0;-0.0;0}", r);

        Assert.Empty(r.Violations);
        Assert.DoesNotContain(r.AccessUnverified, s => s.StartsWith("USB1:", StringComparison.Ordinal));
    }
}
