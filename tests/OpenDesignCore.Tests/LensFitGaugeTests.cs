using OpenDesignCore.Models;
using OpenDesignCore.Runs;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// The gauge exists to answer one question — what nominal diameter produces a
/// hole this printer's output actually fits — so the tests are about the ways
/// it could answer that question wrongly while exporting cleanly.
/// </summary>
public sealed class LensFitGaugeTests : IDisposable
{
    private readonly string _strTempDir =
        Directory.CreateTempSubdirectory("odc-gauge-tests").FullName;

    public void Dispose() => Directory.Delete(_strTempDir, recursive: true);

    private string StrArtifacts => Path.Combine(_strTempDir, "artifacts");
    private string StrLedger => Path.Combine(_strTempDir, "ledger.db");

    /// <summary>
    /// A coarse ladder, so the tests run at a voxel size that does not take
    /// minutes. The default 0.25 mm step needs a 0.0625 mm voxel and produces a
    /// 3.6-million-triangle mesh; a 1.0 mm step needs only 0.25 mm.
    /// </summary>
    private static LensFitGaugeParams OCoarse() =>
        LensFitGaugeParams.ODefault() with
        {
            BoreStepMm = 1.0f,
            BoreCount = 3,
            // A 1 mm step makes the largest bore 16.95 mm, which no longer fits
            // the default 24 mm spacing. The spacing guard caught this fixture
            // before it caught anything else -- correctly, which is why the
            // fixture moved rather than the guard.
            BoreSpacingMm = 28.0f,
        };

    private LensFitGaugeRunResult ORun(LensFitGaugeParams? oParams = null, float? fVoxelMm = null)
    {
        LensFitGaugeParams oUse = oParams ?? OCoarse();
        return LensFitGaugeRun.Execute(
            oUse,
            fVoxelMm ?? LensFitGaugeModel.FResolutionFloorMm(oUse),
            StrArtifacts, StrLedger, "test-commit");
    }

    // ── the floor is set by the step, not the plate ──────────────────────────

    [Fact]
    public void TheFloorIsAQuarterOfTheStepBetweenBores()
    {
        // The plate is 2.5 mm and the smallest bore is ~15 mm, so every obvious
        // candidate feature is far coarser than the thing that actually matters:
        // the 0.25 mm difference between neighbouring bores. Getting this wrong
        // yields a gauge whose rungs are indistinguishable, which reports a fit
        // against a diameter it did not make.
        LensFitGaugeParams o = LensFitGaugeParams.ODefault();
        Assert.Equal(o.BoreStepMm / 4f, LensFitGaugeModel.FResolutionFloorMm(o), 5);
        Assert.True(
            LensFitGaugeModel.FResolutionFloorMm(o) < o.PlateThicknessMm / 2f,
            "the step must bind before the plate thickness does, or the floor is measuring "
            + "the wrong feature");
    }

    [Fact]
    public void AVoxelThatCannotResolveTheStepIsRefused()
    {
        LensFitGaugeParams o = OCoarse();
        float fTooCoarse = LensFitGaugeModel.FResolutionFloorMm(o) * 2f;
        ResolutionFloorException oEx = Assert.Throws<ResolutionFloorException>(
            () => ORun(o, fTooCoarse));
        Assert.Contains("wrong order", oEx.Message);
    }

    // ── the ladder ───────────────────────────────────────────────────────────

    [Fact]
    public void TheLadderClimbsAndStartsAtTheMeasuredBarrel()
    {
        LensFitGaugeParams o = LensFitGaugeParams.ODefault();

        // Starts at the barrel as measured on 2026-09-25, because a printed
        // hole comes out undersize far more often than over, so the ladder only
        // needs to climb.
        Assert.Equal(14.95f, LensFitGaugeModel.FBoreDiameterMm(o, 0));

        for (int n = 1; n < o.BoreCount; n++)
        {
            Assert.True(
                LensFitGaugeModel.FBoreDiameterMm(o, n)
                    > LensFitGaugeModel.FBoreDiameterMm(o, n - 1),
                "bores must increase monotonically or the tally numbering means nothing");
        }
    }

    [Fact]
    public void BoresThatWouldBreakIntoEachOtherAreRefused()
    {
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => ORun(OCoarse() with { BoreSpacingMm = 16.0f }));
        Assert.Contains("measure a slot", oEx.Message);
    }

    [Fact]
    public void ATallyRowTooWideForItsSpacingIsRefused()
    {
        // The tallies are the only thing identifying a bore. If they merge, the
        // gauge still fits a barrel and can no longer say which rung did.
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => ORun(OCoarse() with
            {
                BoreCount = 8,
                TallyDotMm = 3.0f,
                // Spacing generous enough that the bore-collision guard does not
                // fire first; this test is about the tallies.
                BoreSpacingMm = 40.0f,
            }));
        Assert.Contains("stop identifying anything", oEx.Message);
    }

    [Fact]
    public void ALadderNeedsMoreThanOneRung()
    {
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => ORun(OCoarse() with { BoreCount = 1 }));
        Assert.Contains("at least two bores", oEx.Message);
    }

    // ── what it actually produced ────────────────────────────────────────────

    [Fact]
    public void TheExportedPlateIsTheSizeItWasAskedFor()
    {
        LensFitGaugeParams o = OCoarse();
        LensFitGaugeRunResult oResult = ORun(o);
        float fTol = 2 * LensFitGaugeModel.FResolutionFloorMm(o);

        Assert.Equal(LensFitGaugeModel.FOuterXMm(o), oResult.Geometry.BBoxXMm, fTol);
        Assert.Equal(LensFitGaugeModel.FOuterYMm(o), oResult.Geometry.BBoxYMm, fTol);
        Assert.Equal(o.PlateThicknessMm, oResult.Geometry.BBoxZMm, fTol);
    }

    [Fact]
    public void TheBoresRemovedTheMaterialTheyShouldHave()
    {
        // The bounding box is identical whether or not a single hole was cut,
        // so the check is on volume against the analytic bore total. The
        // model's Validate enforces this too; this asserts the figure rather
        // than only that it did not throw.
        LensFitGaugeParams o = OCoarse();
        LensFitGaugeRunResult oResult = ORun(o);

        double fSolid = (double)LensFitGaugeModel.FOuterXMm(o)
                      * LensFitGaugeModel.FOuterYMm(o)
                      * o.PlateThicknessMm;
        double fExpectedRemoved = LensFitGaugeModel.FExpectedBoreVolumeCubicMm(o);
        double fActualRemoved = fSolid - oResult.Geometry.VolumeCubicMm;

        double fSmallestBore =
            Math.PI * Math.Pow(o.BoreStartMm / 2.0, 2) * o.PlateThicknessMm;

        Assert.True(
            Math.Abs(fActualRemoved - fExpectedRemoved) < fSmallestBore / 2.0,
            $"bores removed {fActualRemoved:F1} mm³, expected {fExpectedRemoved:F1} mm³ "
            + $"(bound: half of the smallest bore, {fSmallestBore / 2.0:F1} mm³)");
    }

    [Fact]
    public void TwoRunsOfTheSameInputsAreByteIdentical()
    {
        LensFitGaugeRunResult oA = ORun();
        LensFitGaugeRunResult oB = ORun();
        Assert.Equal(oA.ArtifactSha256, oB.ArtifactSha256);
        Assert.Equal(oA.ProvenanceSha256, oB.ProvenanceSha256);
    }

    [Fact]
    public void TheSidecarSpellsOutTheLadderAndCarriesNoTimestamp()
    {
        // "Bore three fits" is only a measurement if the nominal ladder travels
        // with the object. Days later, nothing else can say what bore three was.
        LensFitGaugeRunResult oResult = ORun();
        string strSidecar = File.ReadAllText(
            Path.Combine(
                StrArtifacts,
                oResult.ProvenanceSha256[..2],
                oResult.ProvenanceSha256 + ".provenance.json"));

        Assert.DoesNotContain("created", strSidecar);
        Assert.Contains("tally_dots", strSidecar);
        Assert.Contains("nominal_diameter_mm", strSidecar);
        Assert.Contains("THIS PRINTER", strSidecar);
    }
}
