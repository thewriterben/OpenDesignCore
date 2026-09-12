using System.Numerics;
using PicoGK;

namespace OpenDesignCore.Import;

/// <summary>
/// Whether an imported mesh is a closed manifold — the property voxelisation
/// needs and the one this boundary claimed to check since 2026-08-15 without
/// ever checking it (ADR-0021).
///
/// <b>Why this is topology and not geometry.</b> ADR-0001 puts geometry
/// algorithms upstream in PicoGK/ShapeKernel, and that rule holds. Counting how
/// many faces meet at each edge is not a geometric operation: nothing is
/// intersected, offset, or meshed. It is the same category as the ASCII STL
/// parser next door — reading the structure of a file we were handed.
///
/// <b>Why vertices are welded by exact position.</b> An STL has no shared
/// vertices: every triangle carries its own three, and PicoGK's loader does not
/// weld them. Measured 2026-09-11 on a closed sphere — 44,820 vertices against
/// 14,940 triangles, a ratio of exactly 3.000, and every one of its 44,820
/// edges reported as a boundary. An index-based check is therefore meaningless
/// on anything loaded from STL.
///
/// Welding uses exact <see cref="Vector3"/> equality, deliberately. A shared
/// vertex written once by one writer comes back bitwise identical, so a
/// tolerance buys nothing here — and a tolerance is exactly what CONTRIBUTING
/// rejects ("a hard-coded epsilon"). Verified on the same sphere: 44,820 raw
/// vertices weld to 7,472 unique, giving 22,410 edges with zero boundary and
/// zero non-manifold, and V − E + F = 2 as a closed genus-0 surface must.
///
/// A mesh whose vertices were written by a different tool at a different
/// precision may fail to weld, and will be reported as open. That is the safe
/// direction: it refuses loudly rather than voxelising something it cannot
/// vouch for.
/// </summary>
public sealed record MeshTopologyReport
{
    public required int RawVertices { get; init; }
    public required int WeldedVertices { get; init; }
    public required int Triangles { get; init; }
    public required int Edges { get; init; }

    /// <summary>Edges with exactly one adjacent face — a hole.</summary>
    public required int BoundaryEdges { get; init; }

    /// <summary>Edges with three or more adjacent faces — self-intersection or a fin.</summary>
    public required int NonManifoldEdges { get; init; }

    public bool IsClosedManifold => BoundaryEdges == 0 && NonManifoldEdges == 0;

    /// <summary>V − E + F. Reported rather than asserted: it is a useful signal
    /// (2 for a sphere, 0 for a torus) and not a validity test, since a
    /// legitimate part may have any genus.</summary>
    public int EulerCharacteristic => WeldedVertices - Edges + Triangles;
}

public static class MeshTopology
{
    public static MeshTopologyReport OAnalyse(Mesh msh)
    {
        int nVerts = msh.nVertexCount();
        Dictionary<Vector3, int> oWeld = new(nVerts);
        int[] anRemap = new int[nVerts];

        for (int i = 0; i < nVerts; i++)
        {
            Vector3 vec = msh.vecVertexAt(i);
            if (!oWeld.TryGetValue(vec, out int nId))
            {
                nId = oWeld.Count;
                oWeld[vec] = nId;
            }
            anRemap[i] = nId;
        }

        int nTris = msh.nTriangleCount();
        Dictionary<(int, int), int> oEdges = new(nTris * 3 / 2);

        for (int i = 0; i < nTris; i++)
        {
            Triangle oTri = msh.oTriangleAt(i);
            int a = anRemap[oTri.A], b = anRemap[oTri.B], c = anRemap[oTri.C];
            AddEdge(oEdges, a, b);
            AddEdge(oEdges, b, c);
            AddEdge(oEdges, c, a);
        }

        int nBoundary = 0, nNonManifold = 0;
        foreach (int nCount in oEdges.Values)
        {
            if (nCount == 1) nBoundary++;
            else if (nCount > 2) nNonManifold++;
        }

        return new MeshTopologyReport
        {
            RawVertices = nVerts,
            WeldedVertices = oWeld.Count,
            Triangles = nTris,
            Edges = oEdges.Count,
            BoundaryEdges = nBoundary,
            NonManifoldEdges = nNonManifold,
        };
    }

    private static void AddEdge(Dictionary<(int, int), int> oEdges, int a, int b)
    {
        (int, int) oKey = a < b ? (a, b) : (b, a);
        oEdges[oKey] = oEdges.GetValueOrDefault(oKey) + 1;
    }
}
