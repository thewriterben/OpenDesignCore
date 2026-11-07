using System.Numerics;
using OpenDesignCore.Data;
using OpenDesignCore.Fit;
using OpenDesignCore.Models;
using PicoGK;
using Xunit;
using Xunit.Abstractions;

namespace OpenDesignCore.Tests;

/// <summary>
/// The assembly check (ADR-0024) on synthetic parts whose answer is known by
/// construction, plus the two PicoGK behaviours it rests on — measured, not
/// read off a signature (CLAUDE.md: a dependency's signature is a claim).
///
/// <para>
/// Every value here is synthetic and says so in its citation. The retroactive
/// cases against the real camera board live in
/// <c>tests/reference/RetroactiveFixtureCheckTest.cs</c>.
/// </para>
/// </summary>
public sealed class AssemblyCheckTests(ITestOutputHelper oOutput) : IDisposable
{
    private const float Voxel = 0.2f;
    private const float Tol = 0.4f;

    private readonly string _strDir = Directory.CreateTempSubdirectory("odc-fit-tests").FullName;

    public void Dispose() => Directory.Delete(_strDir, recursive: true);

    // ── the PicoGK behaviour the check depends on ────────────────────────────

    [Fact]
    public void IntersectionVolumeIsTheOverlapAndLeavesTheOperandsAlone()
    {
        // Two 10 mm cubes offset by 4 mm in x overlap in a 6 x 10 x 10 slab.
        using Library oLib = new(Voxel);
        Voxels voxA = new(Utils.mshCreateCube(oLib, new Vector3(10), Vector3.Zero));
        Voxels voxB = new(Utils.mshCreateCube(oLib, new Vector3(10), new Vector3(4, 0, 0)));
        voxA.CalculateProperties(out float fBefore, out BBox3 _);

        Voxels voxI = voxA.voxBoolIntersect(voxB);
        voxI.CalculateProperties(out float fOverlap, out BBox3 _);
        voxA.CalculateProperties(out float fAfter, out BBox3 _);

        const double fExact = 6 * 10 * 10;
        double fBand = (2 * (6 * 10) + 4 * (10 * 10)) * Voxel; // one voxel over the slab's surface
        oOutput.WriteLine($"overlap {fOverlap:F3} mm³ vs {fExact}, band {fBand:F1}; A {fBefore:F3} -> {fAfter:F3}");
        Assert.InRange(fOverlap, fExact - fBand, fExact + fBand);
        Assert.Equal(fBefore, fAfter);
    }

    [Fact]
    public void DisjointShapesIntersectToNothing()
    {
        using Library oLib = new(Voxel);
        Voxels voxA = new(Utils.mshCreateCube(oLib, new Vector3(10), Vector3.Zero));
        Voxels voxB = new(Utils.mshCreateCube(oLib, new Vector3(10), new Vector3(10 + 2 * Tol, 0, 0)));
        Assert.True(voxA.voxBoolIntersect(voxB).bIsEmpty());
    }

    [Fact]
    public void AClippedBeamIsAFlatEndedCylinder()
    {
        // voxLatticeBeam has hemispherical ends; the check clips them to the
        // registry's flat-ended cylinder. Radius 3 over 10 mm: without the
        // clip it would reach 16 mm and hold two extra hemispheres.
        using Library oLib = new(Voxel);
        Voxels voxBeam = Voxels.voxLatticeBeam(oLib, Vector3.Zero, 3f, new Vector3(0, 0, 10), 3f);
        Voxels voxSpan = new(Utils.mshCreateCube(oLib, new Vector3(8, 8, 10), new Vector3(0, 0, 5)));
        Voxels voxCyl = voxBeam.voxBoolIntersect(voxSpan);
        voxCyl.CalculateProperties(out float fVol, out BBox3 oBox);

        double fExact = Math.PI * 9 * 10;
        double fBand = (2 * Math.PI * 3 * 10 + 2 * Math.PI * 9) * Voxel;
        oOutput.WriteLine($"cylinder {fVol:F3} mm³ vs {fExact:F3}, band {fBand:F1}; z extent {oBox.vecSize().Z:F3}");
        Assert.InRange(fVol, fExact - fBand, fExact + fBand);
        Assert.InRange(oBox.vecSize().Z, 10 - 2 * Voxel, 10 + 2 * Voxel);
    }

    // ── placement ────────────────────────────────────────────────────────────

    [Fact]
    public void AMirrorPlacementIsRefused()
    {
        ArgumentException oEx = Assert.Throws<ArgumentException>(() => PartPlacement.OLanding(
            Axis.NegX, Axis.PosY, Axis.PosZ, [0, 0, 0], Vector3.Zero, "test"));
        Assert.Contains("mirror", oEx.Message);
    }

    [Fact]
    public void LandingPutsThePartPointOnTheFixturePoint()
    {
        // The camera mounts' orientation: board up the fixture's +Z, board
        // normal along +Y, so +x must go to -X to stay a rotation.
        PartPlacement o = PartPlacement.OLanding(
            Axis.NegX, Axis.PosZ, Axis.PosY, [14, 0, 0.6], new Vector3(0, 0, 7), "test");
        Assert.Equal(new Vector3(0, 0, 7), o.VecMap([14, 0, 0.6]));
        Assert.Equal(new Vector3(-1, 5, 7 + 2), o.VecMap([15, 2, 5.6]));
        Assert.Equal(Axis.NegZ, o.MapAxis(Axis.NegY));
    }

    // ── the check itself, on a synthetic board ───────────────────────────────

    /// <summary>A 20 x 30 x 1 board with one edge connector at y = 0 whose access runs -y.</summary>
    private PartFeatures OBoard(string strAccess = """{ "direction": "-y", "depth_mm": 5, "source": "synthetic" }""")
    {
        TestRegistry.StrWrite(_strDir);
        TestRegistry.StrWriteEntry(_strDir, "boards", "fixture-featured.json", $$"""
            {
              "schema_version": 0, "id": "boards/fixture-featured", "namespace": "boards",
              "name": "fixture-featured", "source": { "citation": "test fixture - synthetic" },
              "features": {
                "source": { "citation": "test fixture - synthetic", "model_sha256": "{{new string('0', 64)}}", "revision": "T1" },
                "frame": "synthetic: board corner at origin, z up",
                "not_modelled": ["synthetic gap statement"],
                "items": [
                  { "id": "Board", "kind": "board", "source": "synthetic",
                    "box_mm": { "min_mm": [0, 0, 0], "max_mm": [20, 30, 1] } },
                  { "id": "J1", "kind": "connector", "source": "synthetic",
                    "box_mm": { "min_mm": [7, -1, 1], "max_mm": [13, 5, 4] },
                    "access": {{strAccess}} }
                ]
              }
            }
            """);
        List<string> aErrors = [];
        PartFeatures? o = PartsRegistry.OLoadFeatures(_strDir, "boards/fixture-featured", aErrors);
        Assert.True(o is not null, string.Join("\n", aErrors));
        return o!;
    }

    private static readonly PartPlacement s_oIdentity = new()
    {
        PartXTo = Axis.PosX,
        PartYTo = Axis.PosY,
        PartZTo = Axis.PosZ,
        OriginMm = Vector3.Zero,
        Basis = "identity (test)",
    };

    private static AssemblyCheckResult ORun(PartFeatures o, Vector3 vecBoxMin, Vector3 vecBoxMax, IReadOnlyCollection<string>? aAccept = null)
    {
        using Library oLib = new(Voxel);
        Voxels voxFixture = new(Utils.mshCreateCube(oLib, vecBoxMax - vecBoxMin, (vecBoxMin + vecBoxMax) / 2f));
        return AssemblyCheck.Check(oLib, voxFixture, o, s_oIdentity, Tol, Voxel, aAccept);
    }

    [Fact]
    public void AFixtureThroughTheConnectorIsAContactViolation()
    {
        AssemblyCheckResult r = ORun(OBoard(), new Vector3(8, 1, 2), new Vector3(12, 3, 10));
        AssemblyViolation v = Assert.Single(r.Violations);
        Assert.Equal(("J1", "contact"), (v.FeatureId, v.Kind));
        Assert.False(r.BPassed);
    }

    [Fact]
    public void AFixtureInFrontOfThePlugIsAnAccessViolationOnly()
    {
        // A wall 2 mm in front of the connector face: touches nothing, blocks the plug.
        AssemblyCheckResult r = ORun(OBoard(), new Vector3(0, -5, 0), new Vector3(20, -3, 5));
        AssemblyViolation v = Assert.Single(r.Violations);
        Assert.Equal(("J1", "access"), (v.FeatureId, v.Kind));
    }

    [Fact]
    public void AFixtureClearOfEverythingPasses()
    {
        AssemblyCheckResult r = ORun(OBoard(), new Vector3(0, 10, -6), new Vector3(20, 30, -2));
        Assert.Empty(r.Violations);
        Assert.True(r.BPassed);
        Assert.Equal(["synthetic gap statement"], r.NotModelled);
    }

    [Fact]
    public void TouchingWithinTheToleranceIsNotAViolation()
    {
        // Flush against the board's underside: zero gap, zero interference.
        AssemblyCheckResult r = ORun(OBoard(), new Vector3(0, 10, -4), new Vector3(20, 30, 0));
        Assert.Empty(r.Violations);
    }

    [Fact]
    public void SinkingPastTheToleranceIsAViolation()
    {
        // 2.5 tolerances into the board: this is the v0.2 base under the USB body.
        AssemblyCheckResult r = ORun(OBoard(), new Vector3(0, 10, -4), new Vector3(20, 30, 2.5f * Tol));
        Assert.Contains(r.Violations, v => v.FeatureId == "Board" && v.Kind == "contact");
    }

    [Fact]
    public void AThinItemIsCheckedAtAReducedToleranceAndSaysSo()
    {
        // The 1 mm board is thinner than 2 x 0.4 mm tolerance + 2 voxels, so
        // its z shrink drops to 0.3 mm — still checked, and recorded as such.
        AssemblyCheckResult r = ORun(OBoard(), new Vector3(0, 10, -6), new Vector3(20, 30, -2));
        Assert.Contains(r.ReducedTolerance, s => s.StartsWith("Board:", StringComparison.Ordinal));
        Assert.True(r.BPassed);
    }

    [Fact]
    public void AnItemUnderTwoVoxelsIsTooSmallAndFailsTheCheck()
    {
        TestRegistry.StrWrite(_strDir);
        TestRegistry.StrWriteEntry(_strDir, "boards", "fixture-thin.json", $$"""
            {
              "schema_version": 0, "id": "boards/fixture-thin", "namespace": "boards",
              "name": "fixture-thin", "source": { "citation": "test fixture - synthetic" },
              "features": {
                "source": { "citation": "synthetic", "model_sha256": "{{new string('0', 64)}}", "revision": "T1" },
                "frame": "synthetic", "not_modelled": [],
                "items": [ { "id": "R1", "kind": "component", "source": "synthetic",
                             "box_mm": { "min_mm": [0, 0, 0], "max_mm": [1, 0.5, 0.3] } } ]
              }
            }
            """);
        List<string> aErrors = [];
        PartFeatures o = PartsRegistry.OLoadFeatures(_strDir, "boards/fixture-thin", aErrors)!;
        AssemblyCheckResult r = ORun(o, new Vector3(5), new Vector3(6));
        Assert.Single(r.TooSmallToCheck);
        Assert.False(r.BPassed);
    }

    [Fact]
    public void UnknownAccessIsReportedAndFailsTheCheck()
    {
        PartFeatures o = OBoard("""{ "direction": "-y", "depth_mm": null, "source": "TODO(source): synthetic" }""");
        AssemblyCheckResult r = ORun(o, new Vector3(0, 10, -6), new Vector3(20, 30, -2));
        Assert.Empty(r.Violations);
        Assert.Single(r.AccessUnverified);
        Assert.False(r.BPassed);
    }

    [Fact]
    public void AcceptingUnknownAccessIsExplicitAndRecorded()
    {
        PartFeatures o = OBoard("""{ "direction": null, "depth_mm": null, "source": "TODO(source): synthetic" }""");
        AssemblyCheckResult r = ORun(o, new Vector3(0, 10, -6), new Vector3(20, 30, -2), ["J1"]);
        Assert.Empty(r.AccessUnverified);
        Assert.Single(r.AccessAccepted);
        Assert.True(r.BPassed);
        Assert.Throws<ArgumentException>(() => ORun(o, new Vector3(0, 10, -6), new Vector3(20, 30, -2), ["J9"]));
    }

    [Fact]
    public void AToleranceUnderTwoVoxelsIsRefused()
    {
        PartFeatures o = OBoard();
        using Library oLib = new(Voxel);
        Voxels vox = new(Utils.mshCreateCube(oLib, new Vector3(1), new Vector3(50)));
        Assert.Throws<ResolutionFloorException>(
            () => AssemblyCheck.Check(oLib, vox, o, s_oIdentity, 1.5f * Voxel, Voxel));
    }

    // ── the reader ───────────────────────────────────────────────────────────

    [Fact]
    public void AnEntryWithoutFeaturesIsAnErrorNotAClearance()
    {
        TestRegistry.StrWrite(_strDir);
        List<string> aErrors = [];
        Assert.Null(PartsRegistry.OLoadFeatures(_strDir, TestRegistry.StrUnofferedId, aErrors));
        Assert.Contains(aErrors, s => s.Contains("no features"));
    }

    [Fact]
    public void AConnectorWithoutAnAccessDeclarationIsRejected()
    {
        TestRegistry.StrWrite(_strDir);
        TestRegistry.StrWriteEntry(_strDir, "boards", "fixture-silent.json", $$"""
            {
              "schema_version": 0, "id": "boards/fixture-silent", "namespace": "boards",
              "name": "fixture-silent", "source": { "citation": "test fixture - synthetic" },
              "features": {
                "source": { "citation": "synthetic", "model_sha256": "{{new string('0', 64)}}", "revision": "T1" },
                "frame": "synthetic", "not_modelled": [],
                "items": [ { "id": "J1", "kind": "connector", "source": "synthetic",
                             "box_mm": { "min_mm": [0, 0, 0], "max_mm": [1, 1, 1] } } ]
              }
            }
            """);
        List<string> aErrors = [];
        Assert.Null(PartsRegistry.OLoadFeatures(_strDir, "boards/fixture-silent", aErrors));
        Assert.Contains(aErrors, s => s.Contains("must declare access"));
    }

    private List<string> AErrorsFor(string strFeatures)
    {
        TestRegistry.StrWrite(_strDir);
        TestRegistry.StrWriteEntry(_strDir, "boards", "fixture-loose.json", """
            {
              "schema_version": 0, "id": "boards/fixture-loose", "namespace": "boards",
              "name": "fixture-loose", "source": { "citation": "test fixture - synthetic" },
              "features": FEATURES
            }
            """.Replace("FEATURES", strFeatures));
        List<string> aErrors = [];
        Assert.Null(PartsRegistry.OLoadFeatures(_strDir, "boards/fixture-loose", aErrors));
        return aErrors;
    }

    private static string StrFeatures(string strSourceExtra = "", string strItem = "", string strAccess = "")
        => $$"""
            {
              "source": { "citation": "synthetic", "model_sha256": "{{new string('0', 64)}}", "revision": "T1"{{strSourceExtra}} },
              "frame": "synthetic", "not_modelled": [],
              "items": [ { "id": "J1", "kind": "connector", "source": "synthetic"{{strItem}},
                           "box_mm": { "min_mm": [0, 0, 0], "max_mm": [1, 1, 1] },
                           "access": { "direction": "-y", "depth_mm": null, "source": "synthetic"{{strAccess}} } } ]
            }
            """;

    [Fact]
    public void TheGoodLooseFixtureLoads()
    {
        TestRegistry.StrWrite(_strDir);
        TestRegistry.StrWriteEntry(_strDir, "boards", "fixture-loose.json", """
            { "schema_version": 0, "id": "boards/fixture-loose", "namespace": "boards",
              "name": "fixture-loose", "source": { "citation": "test fixture - synthetic" }, "features": FEATURES }
            """.Replace("FEATURES", StrFeatures(", \"url\": \"https://example.invalid/board\"")));
        List<string> aErrors = [];
        Assert.NotNull(PartsRegistry.OLoadFeatures(_strDir, "boards/fixture-loose", aErrors));
        Assert.Empty(aErrors);
    }

    [Fact]
    public void FeatureSourcesThatReferenceSiyuanAreRejected()
    {
        // The same gate main applies to citations (OpenKnowledgeCore ADR-0001).
        string strBlock = "20260106123456-abcdefg";
        Assert.Contains(AErrorsFor(StrFeatures(", \"url\": \"siyuan://blocks/" + strBlock + "\"")),
            s => s.Contains(".source.url references SiYuan"));
        Assert.Contains(AErrorsFor(StrFeatures().Replace("\"citation\": \"synthetic\"", "\"citation\": \"note " + strBlock + "\"")),
            s => s.Contains(".source.citation references SiYuan"));
        Assert.Contains(AErrorsFor(StrFeatures().Replace("\"kind\": \"connector\", \"source\": \"synthetic\"",
            "\"kind\": \"connector\", \"source\": \"http://127.0.0.1:6806/api/block/get\"")),
            s => s.Contains("'J1'.source references SiYuan"));
        Assert.Contains(AErrorsFor(StrFeatures().Replace("\"depth_mm\": null, \"source\": \"synthetic\"",
            "\"depth_mm\": null, \"source\": \"data/" + strBlock + ".sy\"")),
            s => s.Contains("access.source references SiYuan"));
    }

    [Fact]
    public void UnknownKeysInsideNestedBlocksAreRejected()
    {
        Assert.Contains(AErrorsFor(StrFeatures(", \"licence\": \"x\"")), s => s.Contains(".source has unknown key 'licence'"));
        Assert.Contains(AErrorsFor(StrFeatures(strAccess: ", \"depth\": 3")), s => s.Contains(".access has unknown key 'depth'"));
        Assert.Contains(AErrorsFor(StrFeatures().Replace("\"max_mm\": [1, 1, 1]", "\"max_mm\": [1, 1, 1], \"max\": 2")),
            s => s.Contains(".box_mm has unknown key 'max'"));
    }

    [Fact]
    public void MalformedJsonAndWrongKindsAreReportedNotThrown()
    {
        Assert.Contains(AErrorsFor("{ \"source\": "), s => s.Contains("not valid JSON"));
        Assert.Contains(AErrorsFor(StrFeatures().Replace("\"not_modelled\": []", "\"not_modelled\": [3]")),
            s => s.Contains(".not_modelled[0] must be a string"));
        Assert.Contains(AErrorsFor(StrFeatures().Replace("\"id\": \"J1\"", "\"id\": 1")),
            s => s.Contains("id, a known kind and a source are all required"));
        Assert.Contains(AErrorsFor(StrFeatures().Replace("\"direction\": \"-y\"", "\"direction\": 5")),
            s => s.Contains("access.direction must be"));
        Assert.Contains(AErrorsFor(StrFeatures().Replace("\"box_mm\": { \"min_mm\": [0, 0, 0], \"max_mm\": [1, 1, 1] }", "\"box_mm\": 4")),
            s => s.Contains("box_mm must be an object"));

        TestRegistry.StrWrite(_strDir);
        TestRegistry.StrWriteEntry(_strDir, "boards", "fixture-idnum.json", "{ \"id\": 7 }");
        List<string> aErrors = [];
        Assert.Null(PartsRegistry.OLoadFeatures(_strDir, "boards/fixture-idnum", aErrors));
        Assert.Contains(aErrors, s => s.Contains("id does not match"));
    }
}
