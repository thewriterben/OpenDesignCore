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

    /// <summary>
    /// A tetrahedron: the smallest closed manifold, 10 mm along X.
    ///
    /// This was a single triangle until ADR-0021, which was fine when nothing
    /// checked and is not a solid. The import boundary now refuses an open mesh,
    /// and a lone triangle is three boundary edges — so the fixture had to
    /// become something that could actually be voxelised. Worth noting that the
    /// old fixture passed for weeks while describing nothing at all.
    /// </summary>
    private string StrAsciiStl()
    {
        (float, float, float)[] a = [(0, 0, 0), (10, 0, 0), (0, 10, 0), (0, 0, 10)];
        int[][] aFaces = [[0, 2, 1], [0, 1, 3], [0, 3, 2], [1, 2, 3]];

        string strPath = Path.Combine(_strTempDir, "tetra-ascii.stl");
        System.Text.StringBuilder oSb = new();
        oSb.AppendLine("solid odc-test");
        foreach (int[] f in aFaces)
        {
            oSb.AppendLine("  facet normal 0 0 0");
            oSb.AppendLine("    outer loop");
            foreach (int i in f)
                oSb.AppendLine(
                    $"      vertex {a[i].Item1:0.######} {a[i].Item2:0.######} {a[i].Item3:0.######}");
            oSb.AppendLine("    endloop");
            oSb.AppendLine("  endfacet");
        }
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

    // ---- ADR-0021: the closed-mesh requirement, enforced ------------------

    [Fact]
    public void AClosedMeshReportsClosed()
    {
        using Library oLib = new(0.4f);
        Voxels vox = Voxels.voxSphere(oLib, new System.Numerics.Vector3(0, 0, 0), 8.0f);
        MeshTopologyReport oReport = MeshTopology.OAnalyse(vox.mshAsMesh());

        Assert.True(oReport.IsClosedManifold);
        Assert.Equal(0, oReport.BoundaryEdges);
        Assert.Equal(0, oReport.NonManifoldEdges);

        // A closed genus-0 surface. Reported, not required — a legitimate part
        // may be a torus — but on a sphere it is a strong check that the weld
        // and the edge walk are both right.
        Assert.Equal(2, oReport.EulerCharacteristic);
    }

    [Fact]
    public void StlLoadingDoesNotWeldVertices_SoIndicesAloneAreUseless()
    {
        // Pins the measurement the design rests on. If a future PicoGK welds on
        // load, this fails and MeshTopology's weld becomes redundant rather
        // than load-bearing — which is worth being told about.
        using Library oLib = new(0.4f);
        string strStl = StrBinaryStl(oLib);
        Mesh msh = Mesh.mshFromStlFile(strStl, Mesh.EStlUnit.MM, 1.0f, null, oLib);

        Assert.Equal(3 * msh.nTriangleCount(), msh.nVertexCount());

        MeshTopologyReport oReport = MeshTopology.OAnalyse(msh);
        Assert.True(oReport.WeldedVertices < oReport.RawVertices);
        Assert.True(oReport.IsClosedManifold);
    }

    [Fact]
    public void AMeshWithAHoleIsRefused()
    {
        // A cube missing one face: unambiguously open, and exactly the shape of
        // a scan whose underside no camera saw.
        string strPath = Path.Combine(_strTempDir, "open-box.stl");
        WriteOpenBox(strPath);

        using Library oLib = new(0.4f);
        ImportValidationException e = Assert.Throws<ImportValidationException>(
            () => ScanImport.OImport(
                oLib, strPath, Mesh.EStlUnit.MM, 1.0f,
                EScanOrigin.MetrologyScan, null, StrArtifacts));

        Assert.Contains("not a closed manifold", e.Message);
        Assert.Contains("boundary edge", e.Message);
        // The refusal has to say what to do about it, not merely that it failed.
        Assert.Contains("Repair the mesh", e.Message);
    }

    private static void WriteOpenBox(string strPath)
    {
        (float, float, float)[] v =
        [
            (0, 0, 0), (10, 0, 0), (10, 6, 0), (0, 6, 0),
            (0, 0, 4), (10, 0, 4), (10, 6, 4), (0, 6, 4),
        ];
        // Five faces of six — the bottom (0,1,2,3) is deliberately absent.
        int[][] aFaces =
        [
            [4, 5, 6], [4, 6, 7],
            [0, 1, 5], [0, 5, 4],
            [1, 2, 6], [1, 6, 5],
            [2, 3, 7], [2, 7, 6],
            [3, 0, 4], [3, 4, 7],
        ];

        System.Text.StringBuilder oSb = new();
        oSb.AppendLine("solid open-box");
        foreach (int[] f in aFaces)
        {
            oSb.AppendLine("  facet normal 0 0 0");
            oSb.AppendLine("    outer loop");
            foreach (int i in f)
                oSb.AppendLine(
                    $"      vertex {v[i].Item1:0.######} {v[i].Item2:0.######} {v[i].Item3:0.######}");
            oSb.AppendLine("    endloop");
            oSb.AppendLine("  endfacet");
        }
        oSb.AppendLine("endsolid open-box");
        File.WriteAllText(strPath, oSb.ToString());
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
