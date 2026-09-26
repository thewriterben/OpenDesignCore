using OpenDesignCore.Data;
using OpenDesignCore.Fit;
using OpenDesignCore.Models;
using OpenDesignCore.Runs;
using PicoGK;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// The mount holds a board by its lens barrel, so the tests are about the ways
/// that can fail while still exporting cleanly — and about the failure that
/// produced v0.2 in the first place.
///
/// <para>
/// v0.1 gripped the board's long edges and did not fit: the bottom corners
/// carry a USB-C receptacle and two JST connectors exactly where the slots bit.
/// v0.2 then fitted the lens and put a solid base under the USB-C port. Both
/// passed every geometric check here, because those checks compared the
/// fixture with its own specification. Since ADR-0023 the run also checks the
/// fixture against the board's OpenPartsCore features and refuses to export
/// on a collision — so v0.2's run now refuses, geometry is tested at the
/// model, and the run is tested for its refusal. The run's byte-identity and
/// sidecar tests return with the first version that passes.
/// </para>
/// </summary>
public sealed class CameraMountTests : IDisposable
{
    private readonly string _strTempDir =
        Directory.CreateTempSubdirectory("odc-cammount-tests").FullName;

    public void Dispose() => Directory.Delete(_strTempDir, recursive: true);

    private string StrArtifacts => Path.Combine(_strTempDir, "artifacts");
    private string StrLedger => Path.Combine(_strTempDir, "ledger.db");

    /// <summary>Coarse enough to keep tests quick, fine enough to pass the floor.</summary>
    private static float FVoxel() =>
        CameraMountModel.FResolutionFloorMm(CameraMountParams.ODefault());

    /// <summary>Build, validate and discard: for the parameter checks, which throw before any geometry exists.</summary>
    private static void Build(CameraMountParams oParams, float? fVoxelMm = null)
    {
        float fVoxel = fVoxelMm ?? CameraMountModel.FResolutionFloorMm(oParams);
        using Library oLib = new(fVoxel);
        Voxels vox = CameraMountModel.VoxBuild(oLib, oParams, fVoxel);
        CameraMountModel.Validate(vox, oParams, fVoxel);
    }

    // ── the measured board ───────────────────────────────────────────────────

    [Fact]
    public void TheDefaultsAreTheMeasuredBoardAndNotTheDatasheet()
    {
        // LILYGO publish 60 x 32 x 12 mm with four 2 mm holes. None of it
        // describes this board. If someone "corrects" these toward the
        // published figure the fixture stops fitting, and the symptom would be
        // a dead part rather than a red test.
        CameraMountParams o = CameraMountParams.ODefault();
        Assert.Equal(14.95f, o.BarrelDiameterMm);
        Assert.Equal(5.10f, o.BarrelFreeLengthMm);
        Assert.Equal(16.65f, o.LipDiameterMm);
        Assert.Equal(35.00f, o.AxisToBoardBottomMm);
        Assert.Equal(24.00f, o.LensTipFromPcbMm);
    }

    [Fact]
    public void TheUnmeasuredLensPositionIsNullNotAGuess()
    {
        CameraMountParams o = CameraMountParams.ODefault();
        Assert.Null(o.LensAxisFromLowXEdgeMm);
        Assert.Null(o.LensOnLowZFace);
    }

    [Fact]
    public void TheBoreIsDerivedFromTheMeasuredPrinterOffset()
    {
        // 15.20 is not a number someone liked. It is 14.95 plus a measured
        // offset, and writing it out as a literal would let the two drift the
        // next time the offset is re-measured on a finer ladder.
        CameraMountParams o = CameraMountParams.ODefault();
        Assert.Equal(
            o.BarrelDiameterMm + LensFitGaugeModel.MeasuredBoreUndersizeMm,
            o.BoreDiameterMm,
            4);
    }

    [Fact]
    public void TheBoreIsNotTheBarrelDiameter()
    {
        // The single most likely "tidy-up" someone could make to this model is
        // setting the bore to the measured barrel, because they look like they
        // should match. They must not: a printed hole comes out undersize, and
        // the gap is what lens-fit-gauge/0.1 measures.
        CameraMountParams o = CameraMountParams.ODefault();
        Assert.True(
            o.BoreDiameterMm > o.BarrelDiameterMm,
            "the nominal bore must exceed the barrel, or the printed hole will not accept it");
        Assert.True(
            o.BoreDiameterMm < o.LipDiameterMm,
            "the bore must be smaller than the lip, or there is no datum at all");
    }

    // ── the ways it could fail to hold the board ─────────────────────────────

    [Fact]
    public void ALandThickerThanTheBarrelIsFreeIsRefused()
    {
        // The lip would hit the plate before the barrel passed through, holding
        // the board off its own datum — and it would export perfectly.
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => Build(CameraMountParams.ODefault() with { BoreLandMm = 6.0f }));
        Assert.Contains("free length", oEx.Message);
    }

    [Fact]
    public void ABoreThatDoesNotClearTheBarrelIsRefused()
    {
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => Build(CameraMountParams.ODefault() with { BoreDiameterMm = 14.50f }));
        Assert.Contains("does not clear", oEx.Message);
    }

    [Fact]
    public void ABoreTheLipWouldFallThroughIsRefused()
    {
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => Build(CameraMountParams.ODefault() with { BoreDiameterMm = 17.0f }));
        Assert.Contains("no datum at all", oEx.Message);
    }

    [Fact]
    public void APocketThatDoesNotClearTheLipIsRefused()
    {
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => Build(CameraMountParams.ODefault() with { LipPocketMm = 16.0f }));
        Assert.Contains("does not clear", oEx.Message);
    }

    // ── the resolution floor ─────────────────────────────────────────────────

    [Fact]
    public void TheFloorIsSetByTheBoreBecauseTheBoreIsTheDatum()
    {
        CameraMountParams o = CameraMountParams.ODefault();
        Assert.Equal(
            Math.Min(o.BoreDiameterMm / 100f, o.BoreLandMm / 10f),
            CameraMountModel.FResolutionFloorMm(o),
            5);
    }

    [Fact]
    public void AVoxelThatWouldRoundTheBoreIsRefused()
    {
        CameraMountParams o = CameraMountParams.ODefault();
        ResolutionFloorException oEx = Assert.Throws<ResolutionFloorException>(
            () => Build(o, CameraMountModel.FResolutionFloorMm(o) * 2f));
        Assert.Contains("rattles in or will not enter", oEx.Message);
    }

    // ── what it produces ─────────────────────────────────────────────────────

    [Fact]
    public void TheAxisSitsWhereTheBoardsGeometryPutsIt()
    {
        // Base, plus the deliberate gap under the board's bottom edge, plus the
        // measured 35 mm from that edge to the lens centre.
        CameraMountParams o = CameraMountParams.ODefault();
        Assert.Equal(
            o.BaseThicknessMm + o.FloorDropMm + o.AxisToBoardBottomMm,
            CameraMountModel.FAxisHeightMm(o),
            4);
    }

    [Fact]
    public void TheFixtureIsTheSizeItWasAskedFor()
    {
        CameraMountParams o = CameraMountParams.ODefault();
        float fVoxel = FVoxel();
        using Library oLib = new(fVoxel);
        Voxels vox = CameraMountModel.VoxBuild(oLib, o, fVoxel);
        vox.CalculateProperties(out float _, out BBox3 oBox);
        float fTol = 2 * fVoxel;

        Assert.Equal(CameraMountModel.FOuterXMm(o), oBox.vecSize().X, fTol);
        Assert.Equal(CameraMountModel.FOuterYMm(o), oBox.vecSize().Y, fTol);
        Assert.Equal(CameraMountModel.FOuterZMm(o), oBox.vecSize().Z, fTol);
    }

    [Fact]
    public void TheBoreAndPocketWereActuallyCut()
    {
        // A bounding box is identical whether or not the hole exists. The bound
        // is half the bore's own volume, derived from the failure being guarded
        // against rather than chosen — the same reasoning v0.1 used for its
        // slots, which is the one part of v0.1 worth keeping.
        CameraMountParams o = CameraMountParams.ODefault();
        float fVoxel = FVoxel();
        using Library oLib = new(fVoxel);
        Voxels vox = CameraMountModel.VoxBuild(oLib, o, fVoxel);
        vox.CalculateProperties(out float fMeasured, out BBox3 _);

        double fAnalytic = CameraMountModel.FAnalyticVolumeCubicMm(o);
        double fError = Math.Abs(fMeasured - fAnalytic);
        double fBore = Math.PI * Math.Pow(o.BoreDiameterMm / 2.0, 2) * o.BoreLandMm;

        Assert.True(
            fError < fBore / 2.0,
            $"analytic {fAnalytic:F1} mm³, voxel {fMeasured:F1} mm³, deviation {fError:F1} mm³ "
            + $"({fError / fAnalytic * 100:F2} %), bound {fBore / 2.0:F1} mm³ (half the "
            + $"{fBore:F1} mm³ bore). A deviation this large is not discretization — the bore "
            + "or the lip pocket is missing, misplaced, or the wrong size.");
    }

    // ── the run: checked against the board before anything is written ───────

    [Fact]
    public void TheRunRefusesWhileTheLensPositionIsUnmeasured()
    {
        // Synthetic features are enough: the refusal comes before the check,
        // because the board cannot be placed at all.
        TestRegistry.StrWrite(_strTempDir);
        TestRegistry.StrWriteEntry(_strTempDir, "boards", "fixture-board.json", $$"""
            {
              "schema_version": 0, "id": "boards/fixture-board", "namespace": "boards",
              "name": "fixture-board", "source": { "citation": "test fixture - synthetic" },
              "features": {
                "source": { "citation": "synthetic", "model_sha256": "{{new string('0', 64)}}", "revision": "T1" },
                "frame": "synthetic", "not_modelled": [],
                "items": [ { "id": "Board", "kind": "board", "source": "synthetic",
                             "box_mm": { "min_mm": [0, 0, 0], "max_mm": [28, 68, 1.2] } } ]
              }
            }
            """);
        List<string> aErrors = [];
        PartFeatures oF = PartsRegistry.OLoadFeatures(_strTempDir, "boards/fixture-board", aErrors)!;
        CameraMountParams p = CameraMountParams.ODefault();

        ArgumentException oEx = Assert.Throws<ArgumentException>(() => CameraMountRun.Execute(
            p, FVoxel(), oF, 2 * FVoxel(), [], StrArtifacts, StrLedger, "test-commit"));
        Assert.Contains("LensAxisFromLowXEdgeMm is not measured", oEx.Message);
        Assert.False(Directory.Exists(StrArtifacts), "nothing may be written for an unchecked fixture");
    }

    [Fact]
    public void TheRunRefusesV02AndSaysTheUsbPortIsInTheBase()
    {
        // The real board, from the sibling OpenPartsCore checkout. The lens
        // position here is a hypothesis (board centre, FPC1's face); the
        // reference case shows the collision holds under every combination of
        // face and a ±2 mm offset, which is why a hypothesis is enough to test
        // the refusal and not enough to export anything.
        string? strRegistry = TestRegistry.StrSiblingRegistryOrNull();
        if (strRegistry is null)
            return;
        List<string> aErrors = [];
        PartFeatures? oF = PartsRegistry.OLoadFeatures(strRegistry, CameraMountModel.StrPartId, aErrors);
        Assert.True(oF is not null, string.Join("; ", aErrors));
        (double[] aLo, double[] aHi) = oF!.Items.Single(i => i.Id == "Board").OBounds();
        CameraMountParams p = CameraMountParams.ODefault() with
        {
            LensAxisFromLowXEdgeMm = (float)((aHi[0] - aLo[0]) / 2),
            LensOnLowZFace = true,
        };

        AssemblyCheckFailedException oEx = Assert.Throws<AssemblyCheckFailedException>(() => CameraMountRun.Execute(
            p, FVoxel(), oF, 2 * FVoxel(), [], StrArtifacts, StrLedger, "test-commit"));
        Assert.Contains(oEx.Result.Violations, v => v is { FeatureId: "USB1", Kind: "contact" });
        Assert.Contains("USB1", oEx.Message);
        Assert.False(Directory.Exists(StrArtifacts), "a fixture that fails the check is not exported");
    }
}
