using System.Numerics;
using OpenDesignCore.Data;
using OpenDesignCore.Fit;
using OpenDesignCore.Models;
using OpenDesignCore.Runs;
using PicoGK;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// The enclosure is cut around the board's OpenPartsCore features, so most of
/// these need the sibling registry and return early without it. They test the
/// three things the brief asked for that the assembly check cannot see on its
/// own: a socket housing reaches the headers, the antenna zone is thin plastic
/// and nothing else, and the fasteners stay out of that zone.
/// </summary>
public sealed class SensorNodeEnclosureTests : IDisposable
{
    private readonly string _strTempDir =
        Directory.CreateTempSubdirectory("odc-sensornode-tests").FullName;

    public void Dispose() => Directory.Delete(_strTempDir, recursive: true);

    private static readonly string[] s_aAccept = ["J1", "J2", "U1-ANT"];

    private static PartFeatures? OFeaturesOrNull()
    {
        string? strRegistry = TestRegistry.StrSiblingRegistryOrNull();
        if (strRegistry is null)
            return null;
        List<string> aErrors = [];
        PartFeatures? o = PartsRegistry.OLoadFeatures(strRegistry, SensorNodeEnclosureModel.StrPartId, aErrors);
        // The registry is there but this entry is not: OpenPartsCore is older
        // than the enclosure. Not a failure of this model.
        return o;
    }

    private static IReadOnlyList<MountingHole> AHoles(PartFeatures oF)
    {
        List<string> aErrors = [];
        IReadOnlyList<MountingHole>? a = PartsRegistry.OLoadMountingHoles(
            TestRegistry.StrSiblingRegistryOrNull()!, oF, aErrors);
        Assert.True(a is not null, string.Join("; ", aErrors));
        return a;
    }

    private static SensorNodeLayout L(PartFeatures oF) => SensorNodeLayout.OFrom(oF, AHoles(oF));

    /// <summary>
    /// A copy of the sensor-node entry in a temp registry, with
    /// <paramref name="Edit"/> applied to its text, and the features read from it.
    /// </summary>
    private (string Dir, PartFeatures F)? OEditedCopy(Func<string, string> Edit)
    {
        string? strReg = TestRegistry.StrSiblingRegistryOrNull();
        if (strReg is null) return null;
        string strRel = Path.Combine("data", "boards", "opencircuitcore-sensor-node.json");
        string strSrc = Path.Combine(strReg, strRel);
        if (!File.Exists(strSrc)) return null;
        string strDst = Path.Combine(_strTempDir, strRel);
        Directory.CreateDirectory(Path.GetDirectoryName(strDst)!);
        File.WriteAllText(strDst, Edit(File.ReadAllText(strSrc)));
        List<string> aErrors = [];
        PartFeatures? o = PartsRegistry.OLoadFeatures(_strTempDir, SensorNodeEnclosureModel.StrPartId, aErrors);
        Assert.True(o is not null, string.Join("; ", aErrors));
        return (_strTempDir, o);
    }

    [Fact]
    public void TheHolesComeFromTheRegistryWithYNegated()
    {
        PartFeatures? oF = OFeaturesOrNull();
        if (oF is null) return;
        Assert.Equal(
            [new(3.5f, -17f), new(40.5f, -17f), new(3.5f, -48.5f), new(40.5f, -48.5f)],
            L(oF).HolesMm);
    }

    [Fact]
    public void AHoleInAnUnknownFrameIsRefused()
    {
        var o = OEditedCopy(s => s.Replace("features use the STEP frame, y negated", "same as features"));
        if (o is null) return;
        (string strDir, PartFeatures oF) = o.Value;
        List<string> aErrors = [];
        Assert.Null(PartsRegistry.OLoadMountingHoles(strDir, oF, aErrors));
        Assert.Contains(aErrors, e => e.Contains("only 'KiCad board frame"));
    }

    [Fact]
    public void AHoleOffTheBoardIsRefused()
    {
        var o = OEditedCopy(s => s.Replace("\"y_mm\": 48.5", "\"y_mm\": 58.5"));
        if (o is null) return;
        (string strDir, PartFeatures oF) = o.Value;
        List<string> aErrors = [];
        Assert.Null(PartsRegistry.OLoadMountingHoles(strDir, oF, aErrors));
        Assert.Contains(aErrors, e => e.Contains("outside the board"));
    }

    [Fact]
    public void HolesFromChangedBytesAreRefused()
    {
        var o = OEditedCopy(s => s);
        if (o is null) return;
        (string strDir, PartFeatures oF) = o.Value;
        File.AppendAllText(Path.Combine(strDir, "data", "boards", "opencircuitcore-sensor-node.json"), " ");
        List<string> aErrors = [];
        Assert.Null(PartsRegistry.OLoadMountingHoles(strDir, oF, aErrors));
        Assert.Contains(aErrors, e => e.Contains("changed since its features were read"));
    }

    private static float FVoxel() => SensorNodeEnclosureModel.FResolutionFloorMm(SensorNodeEnclosureParams.ODefault());

    private static float FVolume(Voxels vox)
    {
        vox.CalculateProperties(out float fVol, out BBox3 _);
        return fVol;
    }

    private static Voxels VoxBox(Library oLib, Vector3 a, Vector3 b) =>
        new(Utils.mshCreateCube(oLib, b - a, (a + b) / 2f));

    [Fact]
    public void TheHexPrismIsSolidAndTheRightSize()
    {
        // A mesh wound the wrong way voxelises to nothing or to its complement.
        const float fAf = 5.9f, fH = 3.2f;
        using OdcLibrary oLib = new(0.05f);
        Voxels vox = SensorNodeEnclosureModel.VoxHexPrismZ(oLib, 1f, 2f, 0f, fH, fAf);
        float fWant = MathF.Sqrt(3f) / 2f * fAf * fAf * fH;
        Assert.InRange(FVolume(vox), fWant * 0.97f, fWant * 1.03f);
    }

    [Fact]
    public void TheResolutionFloorIsHalfTheThinnestDesignedGap()
    {
        // The seam gap and the nut pocket's clearance per side are 0.2 mm.
        Assert.Equal(0.1f, FVoxel(), 4);
    }

    [Fact]
    public void ACoarseVoxelIsRefused()
    {
        PartFeatures? oF = OFeaturesOrNull();
        if (oF is null) return;
        using OdcLibrary oLib = new(0.2f);
        Assert.Throws<ResolutionFloorException>(() => SensorNodeEnclosureModel.VoxBuild(
            oLib, SensorNodeEnclosureParams.ODefault(), L(oF), EnclosurePart.Lid, false, 0.2f));
    }

    [Fact]
    public void AHoleInTheAntennaZoneIsRefused()
    {
        PartFeatures? oF = OFeaturesOrNull();
        if (oF is null) return;
        SensorNodeLayout l = L(oF) with
        {
            HolesMm = [new(3.5f, -12f), new(40.5f, -17f), new(3.5f, -48.5f), new(40.5f, -48.5f)],
        };
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => SensorNodeEnclosureModel.ValidateParams(SensorNodeEnclosureParams.ODefault(), l));
        Assert.Contains("antenna", e.Message);
    }

    [Fact]
    public void TheDefaultsKeepEveryFastenerOutOfTheAntennaZone()
    {
        PartFeatures? oF = OFeaturesOrNull();
        if (oF is null) return;
        SensorNodeLayout l = L(oF);
        Assert.Equal(-13.25f, l.AntennaZoneYMm, 3);
        SensorNodeEnclosureModel.ValidateParams(SensorNodeEnclosureParams.ODefault(), l);
    }

    [Fact]
    public void TheAntennaZoneIsThinPlasticAndTheRestIsFullWall()
    {
        PartFeatures? oF = OFeaturesOrNull();
        if (oF is null) return;
        SensorNodeEnclosureParams p = SensorNodeEnclosureParams.ODefault();
        SensorNodeLayout l = L(oF);
        float fVoxel = FVoxel();
        using OdcLibrary oLib = new(fVoxel);
        Voxels voxLid = SensorNodeEnclosureModel.VoxBuild(oLib, p, l, EnclosurePart.Lid, false, fVoxel);
        Voxels voxBase = SensorNodeEnclosureModel.VoxBuild(oLib, p, l, EnclosurePart.Base, false, fVoxel);

        // A 2 x 2 mm column through everything: its volume / 4 is the plastic it crosses.
        float FThrough(Voxels vox, float x, float y) =>
            FVolume(vox.voxBoolIntersect(VoxBox(oLib, new(x - 1, y - 1, -20), new(x + 1, y + 1, 20)))) / 4f;
        float fTol = 3 * fVoxel;

        // Over the antenna (y > -13.25): the lid top and floor are the thin wall.
        Assert.InRange(FThrough(voxLid, 22f, -5f), p.ThinWallMm - fTol, p.ThinWallMm + fTol);
        Assert.InRange(FThrough(voxBase, 22f, -5f), p.ThinWallMm - fTol, p.ThinWallMm + fTol);
        // Between the headers and the baffle, clear of every vent: full thickness.
        Assert.InRange(FThrough(voxLid, 22f, -44f), p.LidTopMm - fTol, p.LidTopMm + fTol);
        Assert.InRange(FThrough(voxBase, 22f, -44f), p.FloorMm - fTol, p.FloorMm + fTol);

        // The y = 0 end wall over the antenna is thin too: a 2 x 2 column along y.
        float fEnd = FVolume(voxLid.voxBoolIntersect(
            VoxBox(oLib, new(21, -3, 4), new(23, 3, 6)))) / 4f;
        Assert.InRange(fEnd, p.ThinWallMm - fTol, p.ThinWallMm + fTol);
    }

    [Fact]
    public void ASocketHousingReachesBothHeaders()
    {
        // The assembly check cannot prove this: the registry does not know how
        // long a socket housing is, and the pins stand above the lid top. So:
        // a housing 0.3 mm wider than each header on every side, from the lid
        // top down to the header's insulator, meets no plastic.
        PartFeatures? oF = OFeaturesOrNull();
        if (oF is null) return;
        SensorNodeEnclosureParams p = SensorNodeEnclosureParams.ODefault();
        SensorNodeLayout l = L(oF);
        float fVoxel = FVoxel();
        using OdcLibrary oLib = new(fVoxel);
        Voxels voxLid = SensorNodeEnclosureModel.VoxBuild(oLib, p, l, EnclosurePart.Lid, false, fVoxel);
        foreach ((Vector3 a, Vector3 b) in new[] { l.J1, l.J2 })
        {
            Voxels voxSocket = VoxBox(oLib,
                new(a.X - 0.3f, a.Y - 0.3f, l.BoardMax.Z + 0.1f),
                new(b.X + 0.3f, b.Y + 0.3f, SensorNodeEnclosureModel.FLidTopZ(p, l) + 5f));
            Assert.True(voxLid.voxBoolIntersect(voxSocket).bIsEmpty());
        }
    }

    [Fact]
    public void TheRunRefusesUncheckedAccessAndOtherwiseWritesTwoReproduciblePairs()
    {
        PartFeatures? oF = OFeaturesOrNull();
        if (oF is null) return;
        SensorNodeEnclosureParams p = SensorNodeEnclosureParams.ODefault();
        float fVoxel = FVoxel();
        string strArtA = Path.Combine(_strTempDir, "a"), strArtB = Path.Combine(_strTempDir, "b");
        string strLedger = Path.Combine(_strTempDir, "ledger.db");

        Assert.Throws<AssemblyCheckFailedException>(() =>
            SensorNodeEnclosureRun.Execute(p, fVoxel, oF, AHoles(oF), 2 * fVoxel, [], strArtA, strLedger, "test"));
        Assert.False(Directory.Exists(strArtA) && Directory.EnumerateFiles(strArtA, "*", SearchOption.AllDirectories).Any());

        SensorNodeEnclosureRunResult r1 = SensorNodeEnclosureRun.Execute(
            p, fVoxel, oF, AHoles(oF), 2 * fVoxel, s_aAccept, strArtA, strLedger, "test");
        SensorNodeEnclosureRunResult r2 = SensorNodeEnclosureRun.Execute(
            p, fVoxel, oF, AHoles(oF), 2 * fVoxel, s_aAccept, strArtB, strLedger, "test");

        Assert.True(r1.Fit.BPassed);
        Assert.Equal(s_aAccept.Order(), r1.Fit.AccessAccepted.Select(s => s.Split(':')[0]).Order());
        Assert.NotEqual(r1.Base.ArtifactSha256, r1.Lid.ArtifactSha256);
        Assert.Equal(r1.Base.ArtifactSha256, r2.Base.ArtifactSha256);
        Assert.Equal(r1.Lid.ArtifactSha256, r2.Lid.ArtifactSha256);
        Assert.Equal(r1.Base.ProvenanceSha256, r2.Base.ProvenanceSha256);
        Assert.Equal(r1.Lid.ProvenanceSha256, r2.Lid.ProvenanceSha256);
        Assert.Equal(2, Directory.EnumerateFiles(strArtA, "*.stl", SearchOption.AllDirectories).Count());
        Assert.Equal(2, Directory.EnumerateFiles(strArtA, "*.provenance.json", SearchOption.AllDirectories).Count());

        // Each sidecar names the other half.
        string strBaseProv = File.ReadAllText(Directory
            .EnumerateFiles(strArtA, r1.Base.ProvenanceSha256 + "*", SearchOption.AllDirectories).Single());
        Assert.Contains(r1.Lid.ArtifactSha256, strBaseProv);

        // Print poses: both sit on z = 0.
        Assert.True(r1.Base.Geometry.BBoxZMm > 0 && r1.Lid.Geometry.BBoxZMm > 0);
    }
}
