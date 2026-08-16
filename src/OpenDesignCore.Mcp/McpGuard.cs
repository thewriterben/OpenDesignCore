namespace OpenDesignCore.Mcp;

public sealed class McpGuardException(string strMessage) : Exception(strMessage);

/// <summary>
/// Resource guards for the agent-facing surface. A caller that can name a
/// voxel size can also name one that exhausts the machine: a 0.01 mm voxel on
/// a 30 mm part is ~10^10 voxels. These limits are refusals, not clamps —
/// silently coarsening a request would violate the no-silent-degradation rule
/// as surely as it would in the geometry layer.
/// </summary>
public static class McpGuard
{
    /// <summary>Finest voxel size an MCP caller may request, mm.</summary>
    public const float FMinVoxelMm = 0.05f;

    /// <summary>Coarsest voxel size worth accepting, mm — beyond this nothing is meaningful.</summary>
    public const float FMaxVoxelMm = 5.0f;

    /// <summary>Largest voxel count a request may imply, before it is refused.</summary>
    public const double FMaxVoxelCount = 2.0e9;

    public static void CheckVoxelSize(float fVoxelMm)
    {
        if (float.IsNaN(fVoxelMm) || fVoxelMm <= 0)
            throw new McpGuardException("Voxel size must be a positive number of millimetres.");
        if (fVoxelMm < FMinVoxelMm)
        {
            throw new McpGuardException(
                $"Voxel size {fVoxelMm} mm is finer than the MCP limit {FMinVoxelMm} mm. " +
                "Run it from the CLI if you genuinely need this resolution.");
        }
        if (fVoxelMm > FMaxVoxelMm)
            throw new McpGuardException($"Voxel size {fVoxelMm} mm is coarser than the MCP limit {FMaxVoxelMm} mm.");
    }

    /// <summary>Refuses a request whose bounding volume would exceed the voxel budget.</summary>
    public static void CheckVolume(double fXMm, double fYMm, double fZMm, float fVoxelMm)
    {
        double fCount = (fXMm / fVoxelMm) * (fYMm / fVoxelMm) * (fZMm / fVoxelMm);
        if (fCount > FMaxVoxelCount)
        {
            throw new McpGuardException(
                $"Request implies ~{fCount:E2} voxels, over the MCP budget of {FMaxVoxelCount:E2}. " +
                "Use a coarser voxel size or run it from the CLI.");
        }
    }

    /// <summary>Keeps callers inside the working tree: no traversal, no absolute paths.</summary>
    public static string StrResolveInsideRoot(string strRoot, string strRelative)
    {
        if (Path.IsPathRooted(strRelative))
            throw new McpGuardException("Absolute paths are not accepted; give a path relative to the working root.");

        string strFull = Path.GetFullPath(Path.Combine(strRoot, strRelative));
        string strRootFull = Path.GetFullPath(strRoot);
        if (!strFull.StartsWith(strRootFull, StringComparison.OrdinalIgnoreCase))
            throw new McpGuardException("Path escapes the working root.");
        return strFull;
    }
}
