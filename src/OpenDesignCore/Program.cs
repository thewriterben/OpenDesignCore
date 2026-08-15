using System.Diagnostics;
using System.Reflection;
using OpenDesignCore.Data;
using OpenDesignCore.Models;
using OpenDesignCore.Runs;

// Commands:
//   (none)                      print tool + pinned-stack versions
//   validate-data [dir]         load and validate the reference data store (default: ./data)
//   run-enclosure [options]     run the thin-thread enclosure model
//     --part <id>               part id (default: parts/esp32-s3-wroom-1)
//     --voxel-mm <v>            voxel size in mm, required, no default (ADR-0003)
//     --clearance-mm <v>        cavity clearance per side (default: 0.30)
//     --wall-mm <v>             wall/floor thickness (default: 2.40)
//     --data <dir>              data directory (default: data)
//     --artifacts <dir>         artifact store (default: artifacts)
//     --ledger <path>           ledger database (default: ledger.db)

if (args is ["validate-data", .. string[] aRest])
{
    string strDataDir = aRest is [string strDir, ..] ? strDir : "data";
    try
    {
        DataSet oData = DataStore.LoadAll(strDataDir);
        Console.WriteLine($"OK: {oData.Parts.Count} part(s), {oData.Materials.Count} material(s), all cited.");
        return 0;
    }
    catch (DataValidationException e)
    {
        Console.Error.WriteLine(e.Message);
        return 1;
    }
}

if (args is ["run-enclosure", ..])
{
    Dictionary<string, string> oOpts = [];
    for (int i = 1; i < args.Length - 1; i += 2)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Unexpected argument '{args[i]}'.");
            return 2;
        }
        oOpts[args[i][2..]] = args[i + 1];
    }

    if (!oOpts.TryGetValue("voxel-mm", out string? strVoxel)
        || !float.TryParse(strVoxel, System.Globalization.CultureInfo.InvariantCulture, out float fVoxelMm))
    {
        Console.Error.WriteLine(
            "--voxel-mm is required and takes no default: voxel size is an explicit " +
            "input to every model run (ADR-0003).");
        return 2;
    }

    float FOpt(string strKey, float fDefault)
        => oOpts.TryGetValue(strKey, out string? s)
            ? float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)
            : fDefault;

    try
    {
        EnclosureRunResult oResult = EnclosureRun.Execute(
            strDataDir: oOpts.GetValueOrDefault("data", "data"),
            strPartId: oOpts.GetValueOrDefault("part", "parts/esp32-s3-wroom-1"),
            fVoxelSizeMm: fVoxelMm,
            fClearanceMm: FOpt("clearance-mm", 0.30f),
            fWallMm: FOpt("wall-mm", 2.40f),
            strArtifactsDir: oOpts.GetValueOrDefault("artifacts", "artifacts"),
            strLedgerPath: oOpts.GetValueOrDefault("ledger", "ledger.db"),
            strCommit: StrGitCommit());

        Console.WriteLine($"run {oResult.RunId}: PASS");
        Console.WriteLine($"  artifact   sha256:{oResult.ArtifactSha256}");
        Console.WriteLine($"  provenance sha256:{oResult.ProvenanceSha256}");
        Console.WriteLine($"  stl        {oResult.ArtifactPath}");
        return 0;
    }
    catch (Exception e) when (e is ResolutionFloorException or GeometryValidationException or DataValidationException or ArgumentException)
    {
        Console.Error.WriteLine(e.Message);
        return 1;
    }
}

if (args is ["handoff", ..])
{
    Dictionary<string, string> oOpts = [];
    List<string> aFlags = [];
    for (int i = 1; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Unexpected argument '{args[i]}'.");
            return 2;
        }
        if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            oOpts[args[i][2..]] = args[++i];
        else
            aFlags.Add(args[i][2..]);
    }

    if (!oOpts.TryGetValue("run", out string? strRun) || !long.TryParse(strRun, out long nRunId))
    {
        Console.Error.WriteLine("--run <ledger run id> is required.");
        return 2;
    }
    if (!oOpts.TryGetValue("stage", out string? strStage))
    {
        Console.Error.WriteLine("--stage <dir> is required (the slicing workspace to copy the artifact into).");
        return 2;
    }

    try
    {
        HandoffResult oResult = StudioHandoff.Execute(
            strArtifactsDir: oOpts.GetValueOrDefault("artifacts", "artifacts"),
            strLedgerPath: oOpts.GetValueOrDefault("ledger", "ledger.db"),
            nRunId: nRunId,
            strStageDir: strStage,
            strStudioUrl: oOpts.GetValueOrDefault("studio", "http://localhost:8770").TrimEnd('/'),
            strGcodeFilename: oOpts.GetValueOrDefault("print"),
            bOffline: aFlags.Contains("offline"));

        Console.WriteLine($"handoff {oResult.HandoffId}: {oResult.Status}");
        Console.WriteLine($"  staged  {oResult.StagedStlPath}");
        if (oResult.ProposalId.Length > 0)
        {
            Console.WriteLine($"  proposal {oResult.ProposalId} — awaiting human approval in the studio dashboard");
            if (oResult.WillRun.Length > 0)
                Console.WriteLine($"  will run: {oResult.WillRun}");
        }
        return 0;
    }
    catch (HandoffException e)
    {
        Console.Error.WriteLine(e.Message);
        return 1;
    }
}

string strTool = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
string strPicoGK = typeof(PicoGK.Library).Assembly.GetName().Version?.ToString() ?? "unknown";

Console.WriteLine($"OpenDesignCore {strTool}");
Console.WriteLine($"PicoGK assembly {strPicoGK} (pinned package 2.2.0, ADR-0008)");
Console.WriteLine($"ShapeKernel: compiled from submodule tag ShapeKernel-v2.1.0");
Console.WriteLine();
Console.WriteLine("usage: OpenDesignCore validate-data [dir]");
Console.WriteLine("       OpenDesignCore run-enclosure --voxel-mm <v> [--part <id>] [--clearance-mm <v>]");
Console.WriteLine("                                    [--wall-mm <v>] [--data <dir>] [--artifacts <dir>] [--ledger <path>]");
Console.WriteLine("       OpenDesignCore handoff --run <id> --stage <dir> [--studio <url>] [--print <gcode>]");
Console.WriteLine("                              [--offline] [--artifacts <dir>] [--ledger <path>]");
return 0;

static string StrGitCommit()
{
    try
    {
        ProcessStartInfo oInfo = new("git", "rev-parse HEAD")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using Process? oProc = Process.Start(oInfo);
        if (oProc is null)
            return "unknown";
        string strOut = oProc.StandardOutput.ReadToEnd().Trim();
        return oProc.WaitForExit(3000) && oProc.ExitCode == 0 && strOut.Length == 40
            ? strOut
            : "unknown";
    }
    catch (Exception)
    {
        return "unknown";
    }
}
