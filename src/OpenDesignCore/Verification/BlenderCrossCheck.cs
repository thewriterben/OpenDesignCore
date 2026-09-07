using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using OpenDesignCore.Provenance;

namespace OpenDesignCore.Verification;

/// <summary>Thrown when a cross-check cannot be run at all (as opposed to failing).</summary>
public sealed class CrossCheckException(string strMessage) : Exception(strMessage);

/// <summary>What Blender measured. Millimetres, as the script reports them.</summary>
public sealed record BlenderMeasurement
{
    public required string BlenderVersion { get; init; }
    public required int Vertices { get; init; }
    public required int Faces { get; init; }
    public required double BboxX { get; init; }
    public required double BboxY { get; init; }
    public required double BboxZ { get; init; }
    public required double VolumeCubicMm { get; init; }
    public required int NonManifoldEdges { get; init; }
    public required int BoundaryEdges { get; init; }

    public static BlenderMeasurement OParse(string strJson)
    {
        using JsonDocument oDoc = JsonDocument.Parse(strJson);
        JsonElement r = oDoc.RootElement;
        JsonElement b = r.GetProperty("bbox_mm");
        return new BlenderMeasurement
        {
            BlenderVersion = r.GetProperty("blender").GetString() ?? "unknown",
            Vertices = r.GetProperty("vertices").GetInt32(),
            Faces = r.GetProperty("faces").GetInt32(),
            BboxX = b.GetProperty("x").GetDouble(),
            BboxY = b.GetProperty("y").GetDouble(),
            BboxZ = b.GetProperty("z").GetDouble(),
            VolumeCubicMm = r.GetProperty("volume_cubic_mm").GetDouble(),
            NonManifoldEdges = r.GetProperty("non_manifold_edges").GetInt32(),
            BoundaryEdges = r.GetProperty("boundary_edges").GetInt32(),
        };
    }
}

/// <summary>What the sidecar claimed. Read from the run's provenance record.</summary>
public sealed record SidecarClaims
{
    public required string ArtifactSha256 { get; init; }
    public required double VoxelSizeMm { get; init; }
    public required double BboxX { get; init; }
    public required double BboxY { get; init; }
    public required double BboxZ { get; init; }
    public required double VolumeCubicMm { get; init; }

    public static SidecarClaims OParse(string strJson)
    {
        using JsonDocument oDoc = JsonDocument.Parse(strJson);
        JsonElement r = oDoc.RootElement;
        JsonElement a = r.GetProperty("artifact");
        JsonElement b = a.GetProperty("bbox_mm");
        return new SidecarClaims
        {
            ArtifactSha256 = a.GetProperty("sha256").GetString()
                ?? throw new CrossCheckException("sidecar has no artifact.sha256"),
            VoxelSizeMm = F(r.GetProperty("voxel_size_mm")),
            BboxX = F(b.GetProperty("x")),
            BboxY = F(b.GetProperty("y")),
            BboxZ = F(b.GetProperty("z")),
            VolumeCubicMm = F(a.GetProperty("volume_cubic_mm")),
        };

        // Lengths in sidecars are unit-keyed strings, never floats (CanonicalJson).
        static double F(JsonElement e) => double.Parse(e.GetString()!, CultureInfo.InvariantCulture);
    }
}

/// <summary>One claim, both numbers, and whether they agree within the stated tolerance.</summary>
public sealed record ClaimCheck(string Claim, double Sidecar, double Blender, double Tolerance, string ToleranceKind)
{
    public double Delta => Blender - Sidecar;
    public bool Agrees => ToleranceKind switch
    {
        "mm" => Math.Abs(Delta) <= Tolerance,
        "pct" => Sidecar != 0 && Math.Abs(Delta) / Math.Abs(Sidecar) * 100.0 <= Tolerance,
        _ => false,
    };
}

public sealed record CrossCheckResult
{
    public required long VerificationRunId { get; init; }
    public required string RecordSha256 { get; init; }
    public required bool Passed { get; init; }
    public required IReadOnlyList<ClaimCheck> Claims { get; init; }
    public required bool Manifold { get; init; }
    public required BlenderMeasurement Measurement { get; init; }
}

/// <summary>
/// A second opinion on a run's sidecar from a kernel that shares no code with
/// PicoGK (ADR-0017). Blender measures the exported STL; the numbers are laid
/// beside the sidecar's and each claim is judged against an explicit tolerance.
/// The artifact is never touched; disagreement is recorded, not repaired.
///
/// The pure parts — <see cref="ACompare"/> and <see cref="OBuildRecord"/> — take
/// measurements as data so they are testable without Blender. <see cref="Execute"/>
/// is the only thing that spawns a process.
/// </summary>
public static class BlenderCrossCheck
{
    public const string StrModelId = "blender-crosscheck/0.1";
    public const string StrSchema = "odc/verification/0.1";
    private const string StrResultPrefix = "__ODC_RESULT__";
    private const string StrErrorPrefix = "__ODC_ERROR__";

    /// <summary>The script this build carries, and its hash for the record.</summary>
    public static (string Text, string Sha256) MeasureScript()
    {
        using Stream? oStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("blender_measure.py")
            ?? throw new CrossCheckException("embedded blender_measure.py missing from this build");
        using MemoryStream oMem = new();
        oStream.CopyTo(oMem);
        byte[] ab = oMem.ToArray();
        return (System.Text.Encoding.UTF8.GetString(ab), ArtifactStore.StrSha256(ab));
    }

    /// <summary>
    /// Bounding-box tolerance when the caller gives none: twice the run's voxel
    /// size. A voxel mesh's extent is uncertain by about a voxel on each face;
    /// this is the same bound the repo's own enclosure tests use. Recorded with
    /// its derivation so a reader can see it was derived, not chosen.
    /// </summary>
    public static double FDefaultBboxToleranceMm(double fVoxelSizeMm) => 2.0 * fVoxelSizeMm;

    public static IReadOnlyList<ClaimCheck> ACompare(
        SidecarClaims oClaims, BlenderMeasurement oMeasured, double fBboxTolMm, double fVolumeTolPct)
    {
        if (fBboxTolMm <= 0)
            throw new CrossCheckException("bbox tolerance must be positive (mm)");
        if (fVolumeTolPct <= 0)
            throw new CrossCheckException("volume tolerance must be positive (percent)");
        return
        [
            new ClaimCheck("bbox_mm.x", oClaims.BboxX, oMeasured.BboxX, fBboxTolMm, "mm"),
            new ClaimCheck("bbox_mm.y", oClaims.BboxY, oMeasured.BboxY, fBboxTolMm, "mm"),
            new ClaimCheck("bbox_mm.z", oClaims.BboxZ, oMeasured.BboxZ, fBboxTolMm, "mm"),
            new ClaimCheck("volume_cubic_mm", oClaims.VolumeCubicMm, oMeasured.VolumeCubicMm, fVolumeTolPct, "pct"),
        ];
    }

    public static bool BManifold(BlenderMeasurement m) => m.NonManifoldEdges == 0 && m.BoundaryEdges == 0;

    /// <summary>The verification record: both kernels' numbers side by side, and the verdict per claim.</summary>
    public static Dictionary<string, object?> OBuildRecord(
        long nRunId,
        SidecarClaims oClaims,
        string strSidecarSha256,
        BlenderMeasurement oMeasured,
        IReadOnlyList<ClaimCheck> aClaims,
        double fBboxTolMm,
        string strBboxTolSource,
        double fVolumeTolPct,
        string strBlenderExe,
        string strScriptSha256,
        string strCommit)
    {
        bool bManifold = BManifold(oMeasured);
        bool bPassed = bManifold && aClaims.All(c => c.Agrees);
        return new Dictionary<string, object?>
        {
            ["schema"] = StrSchema,
            ["model"] = StrModelId,
            ["inputs"] = new Dictionary<string, object?>
            {
                ["run_id"] = nRunId,
                ["artifact_sha256"] = oClaims.ArtifactSha256,
                ["provenance_sha256"] = strSidecarSha256,
                ["bbox_tolerance_mm"] = StrF3(fBboxTolMm),
                ["bbox_tolerance_source"] = strBboxTolSource,
                ["volume_tolerance_pct"] = StrF3(fVolumeTolPct),
                ["volume_tolerance_source"] = "declared by the caller",
                ["blender_exe"] = strBlenderExe,
                ["measure_script_sha256"] = strScriptSha256,
            },
            ["claims"] = aClaims.Select(c => (object?)new Dictionary<string, object?>
            {
                ["claim"] = c.Claim,
                ["sidecar"] = StrF3(c.Sidecar),
                ["blender"] = StrF3(c.Blender),
                ["delta"] = StrF3(c.Delta),
                ["tolerance"] = StrF3(c.Tolerance) + (c.ToleranceKind == "pct" ? " %" : " mm"),
                ["agrees"] = c.Agrees,
            }).ToList(),
            ["manifold"] = new Dictionary<string, object?>
            {
                ["non_manifold_edges"] = oMeasured.NonManifoldEdges,
                ["boundary_edges"] = oMeasured.BoundaryEdges,
                ["watertight"] = bManifold,
            },
            ["mesh"] = new Dictionary<string, object?>
            {
                ["vertices"] = oMeasured.Vertices,
                ["faces"] = oMeasured.Faces,
            },
            ["passed"] = bPassed,
            ["caveats"] = new List<object?>
            {
                "Agreement between two kernels on extents and volume is evidence the "
                    + "sidecar describes the artifact; it is not evidence the artifact is "
                    + "correct for its purpose.",
                "Blender's volume is a signed sum over triangles; PicoGK's is voxel-counted. "
                    + "A difference of the order of one voxel shell is expected, which is why "
                    + "the volume tolerance is declared, not defaulted.",
            },
            ["versions"] = new Dictionary<string, object?>
            {
                ["tool"] = Runs.EnclosureRun.StrToolVersion,
                ["blender"] = oMeasured.BlenderVersion,
            },
            ["commit"] = strCommit,
        };
    }

    /// <summary>Run Blender headless over an STL. The only I/O in this type.</summary>
    public static BlenderMeasurement OMeasure(string strBlenderExe, string strStlPath, TimeSpan oTimeout)
    {
        if (!File.Exists(strBlenderExe))
            throw new CrossCheckException($"Blender not found at '{strBlenderExe}'");
        if (!File.Exists(strStlPath))
            throw new CrossCheckException($"artifact not found at '{strStlPath}'");

        (string strScript, _) = MeasureScript();
        string strScriptPath = Path.Combine(Path.GetTempPath(), $"odc-blender-measure-{Guid.NewGuid():N}.py");
        File.WriteAllText(strScriptPath, strScript);
        try
        {
            ProcessStartInfo oPsi = new(strBlenderExe)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (string s in new[] { "--background", "--factory-startup", "--python", strScriptPath, "--", strStlPath })
                oPsi.ArgumentList.Add(s);

            using Process oProc = Process.Start(oPsi)
                ?? throw new CrossCheckException("could not start Blender");
            string strOut = oProc.StandardOutput.ReadToEnd();
            string strErr = oProc.StandardError.ReadToEnd();
            if (!oProc.WaitForExit((int)oTimeout.TotalMilliseconds))
            {
                oProc.Kill(entireProcessTree: true);
                throw new CrossCheckException($"Blender did not finish within {oTimeout.TotalSeconds:F0}s");
            }

            foreach (string strLine in strOut.Split('\n'))
            {
                string t = strLine.Trim();
                if (t.StartsWith(StrResultPrefix, StringComparison.Ordinal))
                    return BlenderMeasurement.OParse(t[StrResultPrefix.Length..]);
                if (t.StartsWith(StrErrorPrefix, StringComparison.Ordinal))
                    throw new CrossCheckException("Blender: " + JsonSerializer.Deserialize<string>(t[StrErrorPrefix.Length..]));
            }
            throw new CrossCheckException(
                $"Blender produced no result line (exit {oProc.ExitCode}).\nstderr: {strErr.Trim()}\nstdout tail: {strOut[^Math.Min(600, strOut.Length)..].Trim()}");
        }
        finally
        {
            if (File.Exists(strScriptPath))
                File.Delete(strScriptPath);
        }
    }

    public static CrossCheckResult Execute(
        string strArtifactsDir,
        string strLedgerPath,
        long nRunId,
        string strBlenderExe,
        double fVolumeTolPct,
        double? fBboxTolMm,
        string strCommit)
    {
        using Ledger oLedger = new(strLedgerPath);
        RunRecord oRun = oLedger.ORunById(nRunId)
            ?? throw new CrossCheckException($"run {nRunId} not found in {strLedgerPath}");

        string strSidecarPath = ArtifactStore.StrPathFor(strArtifactsDir, oRun.ProvenanceSha256, ".provenance.json");
        if (!File.Exists(strSidecarPath))
            throw new CrossCheckException($"run {nRunId}: provenance sidecar missing from the artifact store");
        SidecarClaims oClaims = SidecarClaims.OParse(File.ReadAllText(strSidecarPath));

        string strStlPath = ArtifactStore.StrPathFor(strArtifactsDir, oClaims.ArtifactSha256, ".stl");
        if (!File.Exists(strStlPath))
            throw new CrossCheckException($"run {nRunId}: artifact {oClaims.ArtifactSha256[..12]} is not an STL in the store (cross-check covers STL artifacts)");

        // The artifact must still be what the sidecar says it is.
        string strActual = ArtifactStore.StrSha256(File.ReadAllBytes(strStlPath));
        if (strActual != oClaims.ArtifactSha256)
            throw new CrossCheckException($"run {nRunId}: artifact bytes hash to {strActual[..12]}, sidecar says {oClaims.ArtifactSha256[..12]}");

        BlenderMeasurement oMeasured = OMeasure(strBlenderExe, strStlPath, TimeSpan.FromMinutes(5));

        double fBboxTol;
        string strBboxTolSource;
        if (fBboxTolMm is double f)
        {
            fBboxTol = f;
            strBboxTolSource = "declared by the caller";
        }
        else
        {
            fBboxTol = FDefaultBboxToleranceMm(oClaims.VoxelSizeMm);
            strBboxTolSource = $"2 x voxel_size_mm ({StrF3(oClaims.VoxelSizeMm)})";
        }

        IReadOnlyList<ClaimCheck> aClaims = ACompare(oClaims, oMeasured, fBboxTol, fVolumeTolPct);
        (_, string strScriptSha) = MeasureScript();
        Dictionary<string, object?> oRecord = OBuildRecord(
            nRunId, oClaims, oRun.ProvenanceSha256, oMeasured, aClaims,
            fBboxTol, strBboxTolSource, fVolumeTolPct, strBlenderExe, strScriptSha, strCommit);

        byte[] abRecord = CanonicalJson.Serialize(oRecord);
        string strRecordHash = ArtifactStore.StrStore(strArtifactsDir, abRecord, ".verification.json");
        bool bPassed = (bool)oRecord["passed"]!;

        long nVerifyId = oLedger.NAppend(new RunRecord
        {
            CreatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Model = StrModelId,
            VoxelSizeMm = StrF3(oClaims.VoxelSizeMm),
            InputsJson = System.Text.Encoding.ASCII.GetString(CanonicalJson.Serialize(oRecord["inputs"])),
            VersionsJson = System.Text.Encoding.ASCII.GetString(CanonicalJson.Serialize(oRecord["versions"])),
            ArtifactSha256 = strRecordHash,
            ProvenanceSha256 = strRecordHash,
            // Passed = every claim within its tolerance and the mesh watertight.
            // A statement that the sidecar describes the artifact; not a verdict on the part.
            Passed = bPassed,
        });

        return new CrossCheckResult
        {
            VerificationRunId = nVerifyId,
            RecordSha256 = strRecordHash,
            Passed = bPassed,
            Claims = aClaims,
            Manifold = BManifold(oMeasured),
            Measurement = oMeasured,
        };
    }

    private static string StrF3(double f) => f.ToString("F3", CultureInfo.InvariantCulture);
}
