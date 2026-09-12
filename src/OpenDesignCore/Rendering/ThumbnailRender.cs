using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using OpenDesignCore.Provenance;

namespace OpenDesignCore.Rendering;

/// <summary>Thrown when a thumbnail cannot be rendered at all.</summary>
public sealed class ThumbnailException(string strMessage) : Exception(strMessage);

/// <summary>The settings the render actually used, as the script reports them.</summary>
public sealed record ThumbnailSettings
{
    public required string BlenderVersion { get; init; }
    public required string Engine { get; init; }
    public required int ResolutionPx { get; init; }
    public required string AntiAliasing { get; init; }
    public required string ViewTransform { get; init; }
    public required string Projection { get; init; }
    public required IReadOnlyList<double> ViewDirection { get; init; }
    public required double Margin { get; init; }
    public required double OrthoScale { get; init; }
    public required double ExtentX { get; init; }
    public required double ExtentY { get; init; }
    public required double ExtentZ { get; init; }
    public required int Vertices { get; init; }
    public required int Faces { get; init; }

    public static ThumbnailSettings OParse(string strJson)
    {
        using JsonDocument oDoc = JsonDocument.Parse(strJson);
        JsonElement r = oDoc.RootElement;
        JsonElement e = r.GetProperty("extent_mm");
        return new ThumbnailSettings
        {
            BlenderVersion = r.GetProperty("blender").GetString() ?? "unknown",
            Engine = r.GetProperty("engine").GetString() ?? "unknown",
            ResolutionPx = r.GetProperty("resolution_px").GetInt32(),
            AntiAliasing = r.GetProperty("anti_aliasing").GetString() ?? "unknown",
            ViewTransform = r.GetProperty("view_transform").GetString() ?? "unknown",
            Projection = r.GetProperty("projection").GetString() ?? "unknown",
            ViewDirection = [.. r.GetProperty("view_direction").EnumerateArray().Select(v => v.GetDouble())],
            Margin = r.GetProperty("margin").GetDouble(),
            OrthoScale = r.GetProperty("ortho_scale").GetDouble(),
            ExtentX = e.GetProperty("x").GetDouble(),
            ExtentY = e.GetProperty("y").GetDouble(),
            ExtentZ = e.GetProperty("z").GetDouble(),
            Vertices = r.GetProperty("vertices").GetInt32(),
            Faces = r.GetProperty("faces").GetInt32(),
        };
    }
}

public sealed record ThumbnailResult
{
    public required long ThumbnailRunId { get; init; }
    public required string RecordSha256 { get; init; }
    public required string ImageSha256 { get; init; }
    public required ThumbnailSettings Settings { get; init; }
}

/// <summary>
/// A picture of an artifact, recorded with the settings that produced it
/// (ADR-0022).
///
/// What the record claims: *this PNG is a deterministic orthographic projection
/// of artifact X, by this Blender, with these settings*. It is checkable — render
/// again and compare hashes. What it does not claim, at all, is that the part is
/// right. ADR-0017 kept thumbnails out precisely because a picture that looks
/// correct invites that conclusion, and that reasoning is not repealed here; it
/// is answered by keeping the two record types apart and saying so in every
/// thumbnail record.
///
/// The pure part — <see cref="OBuildRecord"/> — takes settings as data, so it is
/// testable without Blender. <see cref="Execute"/> is the only thing that spawns
/// a process.
/// </summary>
public static class ThumbnailRender
{
    public const string StrModelId = "blender-thumbnail/0.1";
    public const string StrSchema = "odc/thumbnail/0.1";

    private const string StrResultPrefix = "__ODC_RESULT__";
    private const string StrErrorPrefix = "__ODC_ERROR__";

    /// <summary>The script this build carries, and its hash for the record.</summary>
    public static (string Text, string Sha256) RenderScript()
    {
        using Stream? oStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("blender_thumbnail.py")
            ?? throw new ThumbnailException("embedded blender_thumbnail.py missing from this build");
        using MemoryStream oMem = new();
        oStream.CopyTo(oMem);
        byte[] ab = oMem.ToArray();
        return (System.Text.Encoding.UTF8.GetString(ab), ArtifactStore.StrSha256(ab));
    }

    /// <summary>The thumbnail record: the image's hash, and everything needed to produce it again.</summary>
    public static Dictionary<string, object?> OBuildRecord(
        long nRunId,
        string strArtifactSha256,
        string strProvenanceSha256,
        string strImageSha256,
        ThumbnailSettings oSettings,
        string strBlenderExe,
        string strScriptSha256,
        string strCommit)
    {
        return new Dictionary<string, object?>
        {
            ["schema"] = StrSchema,
            ["model"] = StrModelId,
            ["inputs"] = new Dictionary<string, object?>
            {
                ["run_id"] = nRunId,
                ["artifact_sha256"] = strArtifactSha256,
                ["provenance_sha256"] = strProvenanceSha256,
                ["blender_exe"] = strBlenderExe,
                ["render_script_sha256"] = strScriptSha256,
            },
            ["image"] = new Dictionary<string, object?>
            {
                ["sha256"] = strImageSha256,
                ["format"] = "png",
                ["resolution_px"] = oSettings.ResolutionPx,
                ["normalised"] = "non-essential PNG chunks stripped; Blender writes Date and RenderTime, which are a clock",
            },
            // Everything a second render needs to match this one. The camera is a
            // function of the mesh box and these constants -- no framing was chosen.
            ["settings"] = new Dictionary<string, object?>
            {
                ["engine"] = oSettings.Engine,
                ["projection"] = oSettings.Projection,
                ["anti_aliasing"] = oSettings.AntiAliasing,
                ["view_transform"] = oSettings.ViewTransform,
                ["view_direction"] = oSettings.ViewDirection.Select(v => (object?)StrF3(v)).ToList(),
                ["margin"] = StrF3(oSettings.Margin),
                ["ortho_scale"] = StrF3(oSettings.OrthoScale),
            },
            ["mesh"] = new Dictionary<string, object?>
            {
                ["extent_mm"] = new Dictionary<string, object?>
                {
                    ["x"] = StrF3(oSettings.ExtentX),
                    ["y"] = StrF3(oSettings.ExtentY),
                    ["z"] = StrF3(oSettings.ExtentZ),
                },
                ["vertices"] = oSettings.Vertices,
                ["faces"] = oSettings.Faces,
            },
            ["caveats"] = new List<object?>
            {
                "This is a picture, not a measurement. It says an artifact was "
                    + "projected to pixels; it says nothing about whether the artifact "
                    + "is correct, in tolerance, or manufacturable. Use verify-artifact "
                    + "(odc/verification/0.1) for a claim about numbers.",
                "A render agreeing with what you expected to see is not evidence. "
                    + "The eye cannot resolve a tenth of a millimetre, and this view is "
                    + "one fixed direction: anything hidden behind the part is not in it.",
            },
            ["versions"] = new Dictionary<string, object?>
            {
                ["tool"] = Runs.EnclosureRun.StrToolVersion,
                ["blender"] = oSettings.BlenderVersion,
            },
            ["commit"] = strCommit,
        };
    }

    /// <summary>Run Blender headless to produce a PNG. The only process spawn here.</summary>
    public static (byte[] Png, ThumbnailSettings Settings) ORender(
        string strBlenderExe, string strStlPath, TimeSpan oTimeout)
    {
        if (!File.Exists(strBlenderExe))
            throw new ThumbnailException($"Blender not found at '{strBlenderExe}'");
        if (!File.Exists(strStlPath))
            throw new ThumbnailException($"artifact not found at '{strStlPath}'");

        (string strScript, _) = RenderScript();
        string strScriptPath = Path.Combine(Path.GetTempPath(), $"odc-blender-thumb-{Guid.NewGuid():N}.py");
        string strOutPath = Path.Combine(Path.GetTempPath(), $"odc-thumb-{Guid.NewGuid():N}.png");
        File.WriteAllText(strScriptPath, strScript);
        try
        {
            ProcessStartInfo oPsi = new(strBlenderExe)
            {
                // stdin redirected and closed at once -- same reason as
                // BlenderCrossCheck.OMeasure: inherited, Blender blocks on it
                // whenever our own stdin is a pipe nobody closes.
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (string s in new[] { "--background", "--factory-startup", "--python", strScriptPath, "--", strStlPath, strOutPath })
                oPsi.ArgumentList.Add(s);

            using Process oProc = Process.Start(oPsi)
                ?? throw new ThumbnailException("could not start Blender");
            oProc.StandardInput.Close();
            Task<string> oErrTask = oProc.StandardError.ReadToEndAsync();
            string strOut = oProc.StandardOutput.ReadToEnd();
            string strErr = oErrTask.GetAwaiter().GetResult();
            if (!oProc.WaitForExit((int)oTimeout.TotalMilliseconds))
            {
                oProc.Kill(entireProcessTree: true);
                throw new ThumbnailException($"Blender did not finish within {oTimeout.TotalSeconds:F0}s");
            }

            ThumbnailSettings? oSettings = null;
            foreach (string strLine in strOut.Split('\n'))
            {
                string t = strLine.Trim();
                if (t.StartsWith(StrResultPrefix, StringComparison.Ordinal))
                {
                    oSettings = ThumbnailSettings.OParse(t[StrResultPrefix.Length..]);
                    break;
                }
                if (t.StartsWith(StrErrorPrefix, StringComparison.Ordinal))
                    throw new ThumbnailException("Blender: " + JsonSerializer.Deserialize<string>(t[StrErrorPrefix.Length..]));
            }
            if (oSettings is null)
                throw new ThumbnailException(
                    $"Blender produced no result line (exit {oProc.ExitCode}).\nstderr: {strErr.Trim()}\nstdout tail: {strOut[^Math.Min(600, strOut.Length)..].Trim()}");
            if (!File.Exists(strOutPath))
                throw new ThumbnailException("Blender reported success but wrote no image");

            return (PngNormalise.AbStrip(File.ReadAllBytes(strOutPath)), oSettings);
        }
        finally
        {
            if (File.Exists(strScriptPath))
                File.Delete(strScriptPath);
            if (File.Exists(strOutPath))
                File.Delete(strOutPath);
        }
    }

    public static ThumbnailResult Execute(
        string strArtifactsDir,
        string strLedgerPath,
        long nRunId,
        string strBlenderExe,
        string strCommit)
    {
        using Ledger oLedger = new(strLedgerPath);
        RunRecord oRun = oLedger.ORunById(nRunId)
            ?? throw new ThumbnailException($"run {nRunId} not found in {strLedgerPath}");

        string strSidecarPath = ArtifactStore.StrPathFor(strArtifactsDir, oRun.ProvenanceSha256, ".provenance.json");
        if (!File.Exists(strSidecarPath))
            throw new ThumbnailException($"run {nRunId}: provenance sidecar missing from the artifact store");

        string strArtifactSha;
        using (JsonDocument oDoc = JsonDocument.Parse(File.ReadAllText(strSidecarPath)))
            strArtifactSha = oDoc.RootElement.GetProperty("artifact").GetProperty("sha256").GetString()
                ?? throw new ThumbnailException("sidecar has no artifact.sha256");

        string strStlPath = ArtifactStore.StrPathFor(strArtifactsDir, strArtifactSha, ".stl");
        if (!File.Exists(strStlPath))
            throw new ThumbnailException(
                $"run {nRunId}: artifact {strArtifactSha[..12]} is not an STL in the store (thumbnails cover STL artifacts)");

        // The artifact must still be what the sidecar says it is, or the picture
        // would be of something else wearing that run's name.
        string strActual = ArtifactStore.StrSha256(File.ReadAllBytes(strStlPath));
        if (strActual != strArtifactSha)
            throw new ThumbnailException($"run {nRunId}: artifact bytes hash to {strActual[..12]}, sidecar says {strArtifactSha[..12]}");

        (byte[] abPng, ThumbnailSettings oSettings) = ORender(strBlenderExe, strStlPath, TimeSpan.FromMinutes(5));
        string strImageSha = ArtifactStore.StrStore(strArtifactsDir, abPng, ".thumbnail.png");

        (_, string strScriptSha) = RenderScript();
        Dictionary<string, object?> oRecord = OBuildRecord(
            nRunId, strArtifactSha, oRun.ProvenanceSha256, strImageSha,
            oSettings, strBlenderExe, strScriptSha, strCommit);

        byte[] abRecord = CanonicalJson.Serialize(oRecord);
        string strRecordHash = ArtifactStore.StrStore(strArtifactsDir, abRecord, ".thumbnail.json");

        long nThumbId = oLedger.NAppend(new RunRecord
        {
            CreatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Model = StrModelId,
            // A render has no voxel size; it is a projection of an artifact that
            // had one. Recorded as the run's, so the row reads honestly.
            VoxelSizeMm = oRun.VoxelSizeMm,
            InputsJson = System.Text.Encoding.ASCII.GetString(CanonicalJson.Serialize(oRecord["inputs"])),
            VersionsJson = System.Text.Encoding.ASCII.GetString(CanonicalJson.Serialize(oRecord["versions"])),
            ArtifactSha256 = strImageSha,
            ProvenanceSha256 = strRecordHash,
            // `passed` here means the render completed and the image was stored.
            // It is not a verdict on the part, and it never was for any model:
            // for an enclosure run it means the validation gate passed, for a
            // cross-check that the claims agreed. The predicate is per-model and
            // the `model` column names which one.
            //
            // A nullable `passed` would say this more plainly, and was rejected:
            // the column is NOT NULL on an append-only ledger, so it would mean
            // migrating 75 existing rows to express one new model's abstention.
            // The cost lands on the oldest records in the repo to make a picture
            // read better. See ADR-0022.
            Passed = true,
        });

        return new ThumbnailResult
        {
            ThumbnailRunId = nThumbId,
            RecordSha256 = strRecordHash,
            ImageSha256 = strImageSha,
            Settings = oSettings,
        };
    }

    private static string StrF3(double f) => f.ToString("F3", CultureInfo.InvariantCulture);
}
