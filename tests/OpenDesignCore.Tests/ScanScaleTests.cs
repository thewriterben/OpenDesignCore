using OpenDesignCore.Import;
using PicoGK;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// The post-scale reaches the geometry — on both import paths.
///
/// This exists because for a month it did not. PicoGK 2.2.0's
/// `mshFromStlFile` takes a scale argument and does not apply it, while our own
/// ASCII parser did, so a binary STL imported with `--scale 2.0` came out at
/// 1.0 and the sidecar recorded 2.0 as though it had happened. Measured
/// 2026-09-11. Nothing in the suite compared a scaled import against an
/// unscaled one, so nothing noticed.
///
/// Scaling now happens in one place in our own code, after load, for both
/// paths. These are known-answer tests: a sphere twice as big is twice as big.
/// </summary>
public sealed class ScanScaleTests : IDisposable
{
    private readonly string _strTempDir = Path.Combine(
        Path.GetTempPath(), $"odc-scale-{Guid.NewGuid():N}");

    private string StrArtifacts => Path.Combine(_strTempDir, "artifacts");

    public ScanScaleTests() => Directory.CreateDirectory(_strTempDir);

    public void Dispose()
    {
        if (Directory.Exists(_strTempDir))
            Directory.Delete(_strTempDir, recursive: true);
    }

    private string StrBinaryStl(Library oLib)
    {
        string strPath = Path.Combine(_strTempDir, "sphere.stl");
        Voxels voxSphere = Voxels.voxSphere(oLib, new System.Numerics.Vector3(0, 0, 0), 8.0f);
        voxSphere.mshAsMesh().SaveToStlFile(strPath, Mesh.EStlUnit.MM);
        return strPath;
    }

    private string StrAsciiStl()
    {
        string strPath = Path.Combine(_strTempDir, "tri-ascii.stl");
        System.Text.StringBuilder oSb = new();
        oSb.AppendLine("solid odc-test");
        oSb.AppendLine("  facet normal 0 0 1");
        oSb.AppendLine("    outer loop");
        oSb.AppendLine("      vertex 0 0 0");
        oSb.AppendLine("      vertex 10 0 0");
        oSb.AppendLine("      vertex 0 10 1");
        oSb.AppendLine("    endloop");
        oSb.AppendLine("  endfacet");
        oSb.AppendLine("endsolid odc-test");
        File.WriteAllText(strPath, oSb.ToString());
        return strPath;
    }

    [Fact]
    public void BinaryStl_IsActuallyScaled()
    {
        using Library oLib = new(0.4f);
        string strStl = StrBinaryStl(oLib);

        ScanImportResult oOne = ScanImport.OImport(
            oLib, strStl, Mesh.EStlUnit.MM, 1.0f,
            EScanOrigin.MetrologyScan, null, StrArtifacts);
        ScanImportResult oTwo = ScanImport.OImport(
            oLib, strStl, Mesh.EStlUnit.MM, 2.0f,
            EScanOrigin.MetrologyScan, null, StrArtifacts);

        Assert.Equal(oOne.SizeMm.X * 2f, oTwo.SizeMm.X, 3);
        Assert.Equal(oOne.SizeMm.Y * 2f, oTwo.SizeMm.Y, 3);
        Assert.Equal(oOne.SizeMm.Z * 2f, oTwo.SizeMm.Z, 3);
    }

    [Fact]
    public void AsciiStl_IsActuallyScaled()
    {
        using Library oLib = new(0.4f);
        string strStl = StrAsciiStl();

        ScanImportResult oOne = ScanImport.OImport(
            oLib, strStl, Mesh.EStlUnit.MM, 1.0f,
            EScanOrigin.MetrologyScan, null, StrArtifacts);
        ScanImportResult oTwo = ScanImport.OImport(
            oLib, strStl, Mesh.EStlUnit.MM, 2.0f,
            EScanOrigin.MetrologyScan, null, StrArtifacts);

        Assert.Equal(10f, oOne.SizeMm.X, 3);
        Assert.Equal(20f, oTwo.SizeMm.X, 3);
    }

    [Fact]
    public void BothPathsAgreeOnUnitsAndScaleTogether()
    {
        // The two paths compute the same thing by different routes; that they
        // agree is the property that broke.
        using Library oLib = new(0.4f);
        string strAscii = StrAsciiStl();

        ScanImportResult oMm = ScanImport.OImport(
            oLib, strAscii, Mesh.EStlUnit.MM, 2.0f,
            EScanOrigin.MetrologyScan, null, StrArtifacts);
        ScanImportResult oCm = ScanImport.OImport(
            oLib, strAscii, Mesh.EStlUnit.CM, 0.2f,
            EScanOrigin.MetrologyScan, null, StrArtifacts);

        // 10 units × 1 mm × 2.0  ==  10 units × 10 mm × 0.2
        Assert.Equal(oMm.SizeMm.X, oCm.SizeMm.X, 3);
        Assert.Equal(20f, oMm.SizeMm.X, 3);
    }
}
