using System.Diagnostics;
using System.Reflection;
using OpenDesignCore.Data;
using OpenDesignCore.Models;
using OpenDesignCore.Runs;
using OpenDesignCore.Verification;

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

if (args is ["run-cradle", ..])
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

    if (!oOpts.TryGetValue("stl", out string? strStl))
    {
        Console.Error.WriteLine("--stl <path> is required (the scanned mesh).");
        return 2;
    }
    if (!oOpts.TryGetValue("units", out string? strUnits)
        || !Enum.TryParse(strUnits, ignoreCase: true, out PicoGK.Mesh.EStlUnit eUnits)
        || eUnits == PicoGK.Mesh.EStlUnit.AUTO)
    {
        Console.Error.WriteLine(
            "--units <mm|cm|m|in|ft> is required: an STL carries no reliable unit " +
            "information and silent inference is forbidden.");
        return 2;
    }
    if (!oOpts.TryGetValue("voxel-mm", out string? strVoxel)
        || !float.TryParse(strVoxel, System.Globalization.CultureInfo.InvariantCulture, out float fVoxelMm))
    {
        Console.Error.WriteLine("--voxel-mm is required and takes no default (ADR-0003).");
        return 2;
    }

    float FOpt(string strKey, float fDefault)
        => oOpts.TryGetValue(strKey, out string? s)
            ? float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)
            : fDefault;

    try
    {
        CradleRunResult oResult = CradleRun.Execute(
            strStlPath: strStl,
            eUnits: eUnits,
            fPostScale: FOpt("scale", 1.0f),
            fVoxelSizeMm: fVoxelMm,
            fClearanceMm: FOpt("clearance-mm", 0.30f),
            fWallMm: FOpt("wall-mm", 2.40f),
            fSplitFraction: FOpt("split", 0.45f),
            strArtifactsDir: oOpts.GetValueOrDefault("artifacts", "artifacts"),
            strLedgerPath: oOpts.GetValueOrDefault("ledger", "ledger.db"),
            strCommit: StrGitCommit());

        Console.WriteLine($"run {oResult.RunId}: PASS");
        Console.WriteLine($"  scan       sha256:{oResult.ScanSha256}");
        Console.WriteLine($"  artifact   sha256:{oResult.ArtifactSha256}");
        Console.WriteLine($"  provenance sha256:{oResult.ProvenanceSha256}");
        Console.WriteLine($"  stl        {oResult.ArtifactPath}");
        return 0;
    }
    catch (Exception e) when (e is ResolutionFloorException or GeometryValidationException
        or OpenDesignCore.Import.ImportValidationException or ArgumentException)
    {
        Console.Error.WriteLine(e.Message);
        return 1;
    }
}

if (args is ["compare", ..])
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

    if (!oOpts.TryGetValue("design", out string? strDesign))
    {
        Console.Error.WriteLine("--design <stl> is required.");
        return 2;
    }
    bool bHasScan = oOpts.TryGetValue("scan", out string? strScanPath);
    bool bHasMeasured = oOpts.TryGetValue("measured", out string? strMeasured);
    if (bHasScan == bHasMeasured)
    {
        Console.Error.WriteLine(
            "Give exactly one of --scan <stl> or --measured <X>x<Y>x<Z>. They are two "
            + "instruments answering the same question, and silently preferring one "
            + "would hide a disagreement between them.");
        return 2;
    }
    if (!oOpts.TryGetValue("units", out string? strUnitsArg)
        || !Enum.TryParse(strUnitsArg, ignoreCase: true, out PicoGK.Mesh.EStlUnit eCmpUnits)
        || eCmpUnits == PicoGK.Mesh.EStlUnit.AUTO)
    {
        Console.Error.WriteLine("--units <mm|cm|m|in|ft> is required for both meshes.");
        return 2;
    }
    if (!oOpts.TryGetValue("voxel-mm", out string? strCmpVoxel)
        || !float.TryParse(strCmpVoxel, System.Globalization.CultureInfo.InvariantCulture, out float fCmpVoxel))
    {
        Console.Error.WriteLine("--voxel-mm is required and takes no default (ADR-0003).");
        return 2;
    }

    try
    {
        // One declared accuracy, whichever instrument produced the numbers.
        // A caliper and a scanner are both instruments with a stated error,
        // and the machinery downstream does not care which one it was.
        float fAccuracy = oOpts.TryGetValue("instrument-accuracy-mm", out string? strInstr)
            ? float.Parse(strInstr, System.Globalization.CultureInfo.InvariantCulture)
            : oOpts.TryGetValue("scan-accuracy-mm", out string? strAcc)
                ? float.Parse(strAcc, System.Globalization.CultureInfo.InvariantCulture)
                : 0f;

        CompareRunResult oResult;
        if (bHasMeasured)
        {
            float[] aM = CompareRun.AParseMeasured(strMeasured!);
            // Four readings: X, Y, then the shelf and the tall face. The
            // nominal shelf height must be declared, because the design STL
            // knows its overall height but not where the step was put.
            float fZLowNominal = oOpts.TryGetValue("nominal-step-z-mm", out string? strNs)
                ? float.Parse(strNs, System.Globalization.CultureInfo.InvariantCulture)
                : 0f;
            if (aM.Length == 4 && fZLowNominal <= 0)
            {
                Console.Error.WriteLine(
                    "Four measurements given, so --nominal-step-z-mm is required: the design "
                    + "STL records its overall height but not where the shelf was placed, and "
                    + "the shelf's nominal height is what separates first-layer offset from "
                    + "shrinkage. It is printed by run-calibration-block.");
                return 2;
            }

            oResult = CompareRun.ExecuteMeasured(
                strDesign, eCmpUnits, fCmpVoxel,
                aM[0], aM[1], aM.Length == 4 ? aM[3] : aM[2], fAccuracy,
                oOpts.GetValueOrDefault("artifacts", "artifacts"),
                oOpts.GetValueOrDefault("ledger", "ledger.db"),
                StrGitCommit(),
                fMeasuredZLowMm: aM.Length == 4 ? aM[2] : 0f,
                fNominalZLowMm: aM.Length == 4 ? fZLowNominal : 0f);

            if (aM.Length == 3)
            {
                Console.WriteLine(
                    "  note: a single Z reading contains the first-layer squish, which is a "
                    + "constant rather than a percentage. Use a stepped block and four "
                    + "readings to separate them.");
            }
        }
        else
        {
            oResult = CompareRun.Execute(
                strDesign, strScanPath!, eCmpUnits, fCmpVoxel,
                oOpts.GetValueOrDefault("artifacts", "artifacts"),
                oOpts.GetValueOrDefault("ledger", "ledger.db"),
                StrGitCommit(),
                fAccuracy);
        }

        Console.WriteLine($"run {oResult.RunId}: dimensional comparison");
        foreach (AxisDeviation oAxis in oResult.Report.Axes)
        {
            Console.WriteLine(
                $"  {oAxis.Axis}  design {oAxis.DesignMm,8:F3}  scan {oAxis.ScanMm,8:F3}  "
                + $"dev {oAxis.DeviationMm,7:F3} mm ({oAxis.DeviationPct,6:F2} %)");
        }
        Console.WriteLine($"  max |deviation| {oResult.Report.MaxAbsDeviationMm:F3} mm; "
            + $"mean {oResult.Report.MeanDeviationPct:F2} %, spread {oResult.Report.DeviationPctSpread:F2} %");
        // Whether these numbers justify a compensation is a separate judgement
        // with a declared threshold — `compensate` makes it. This line used to
        // decide it here against a hard-coded 0.5 % spread, which is exactly
        // the constant-instead-of-parameter the tolerance rule forbids.
        Console.WriteLine(oResult.Report.WithinScanAccuracy switch
        {
            true => "  within the declared scanner accuracy — no compensation implied",
            false => "  deviation exceeds the declared scanner accuracy; run `compensate` "
                + "--comparison <hash> --max-axis-spread-pct <v> to judge whether one "
                + "factor is defensible",
            null => "  no --scan-accuracy-mm declared: cannot say whether this deviation is "
                + "real or instrument error. Declare it, or treat the numbers as indicative only.",
        });
        Console.WriteLine($"  report sha256:{oResult.ReportSha256}");
        return 0;
    }
    catch (Exception e) when (e is OpenDesignCore.Import.ImportValidationException or ArgumentException)
    {
        Console.Error.WriteLine(e.Message);
        return 1;
    }
}

if (args is ["run-calibration-block", ..])
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

    if (!oOpts.TryGetValue("instrument-accuracy-mm", out string? strAcc)
        || !float.TryParse(strAcc, System.Globalization.CultureInfo.InvariantCulture, out float fInstrAcc))
    {
        Console.Error.WriteLine(
            "--instrument-accuracy-mm is required and takes no default: it is recorded in "
            + "provenance and decides, later, whether a measured deviation is real or "
            + "instrument error. A typical digital caliper is 0.02 mm.");
        return 2;
    }

    CalibrationBlockParams oDefault = CalibrationBlockModel.ODefault();
    float FBlock(string strKey, float fDefault)
        => oOpts.TryGetValue(strKey, out string? s)
            ? float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)
            : fDefault;

    CalibrationBlockParams oBlock = new()
    {
        XMm = FBlock("x-mm", oDefault.XMm),
        YMm = FBlock("y-mm", oDefault.YMm),
        ZMm = FBlock("z-mm", oDefault.ZMm),
        StepZMm = FBlock("step-z-mm", oDefault.StepZMm),
        TallDepthYMm = FBlock("tall-depth-y-mm", oDefault.TallDepthYMm),
    };

    // Defaults to the model's own floor, which is derived from the block's
    // dimensions rather than being a constant. Note it is NOT derived from the
    // instrument accuracy: the comparison uses the exported bounding box, so
    // grid quantisation is measured rather than assumed.
    float fBlockVoxel = oOpts.TryGetValue("voxel-mm", out string? strBv)
        ? float.Parse(strBv, System.Globalization.CultureInfo.InvariantCulture)
        : CalibrationBlockModel.FResolutionFloorMm(oBlock);

    try
    {
        CalibrationBlockRunResult oResult = CalibrationBlockRun.Execute(
            oBlock, fBlockVoxel, fInstrAcc,
            oOpts.GetValueOrDefault("artifacts", "artifacts"),
            oOpts.GetValueOrDefault("ledger", "ledger.db"),
            StrGitCommit());

        Console.WriteLine($"run {oResult.RunId}: PASS  {CalibrationBlockModel.StrModelId}");
        Console.WriteLine(
            $"  nominal    {oBlock.XMm:F2} x {oBlock.YMm:F2} mm, shelf at {oBlock.StepZMm:F2} mm, "
            + $"tall face at {oBlock.ZMm:F2} mm (Z span {oBlock.ZSpanMm:F2} mm)");
        Console.WriteLine(
            $"  exported   {oResult.Geometry.BBoxXMm:F3} x {oResult.Geometry.BBoxYMm:F3} x "
            + $"{oResult.Geometry.BBoxZMm:F3} mm (voxel {fBlockVoxel:F4} mm)");
        Console.WriteLine($"  artifact   sha256:{oResult.ArtifactSha256}");
        Console.WriteLine($"  stl        {oResult.ArtifactPath}");
        Console.WriteLine();
        Console.WriteLine("  Print, cool, then take FOUR readings:");
        Console.WriteLine("    X and Y across the flat faces, a few mm up from the bed.");
        Console.WriteLine("    Z twice: bed to shelf, and bed to the tall face. Both contain the");
        Console.WriteLine("    same first-layer squish, so their difference contains none.");
        Console.WriteLine();
        Console.WriteLine($"    compare --design {oResult.ArtifactPath} --units mm \\");
        Console.WriteLine($"            --voxel-mm {fBlockVoxel:F4} \\");
        Console.WriteLine("            --measured <X>x<Y>x<Zlow>x<Zhigh> \\");
        Console.WriteLine($"            --instrument-accuracy-mm {fInstrAcc}");
        return 0;
    }
    catch (Exception e) when (e is ResolutionFloorException or GeometryValidationException or ArgumentException)
    {
        Console.Error.WriteLine(e.Message);
        return 1;
    }
}

if (args is ["compensate", ..])
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

    if (!oOpts.TryGetValue("comparison", out string? strComparison))
    {
        Console.Error.WriteLine(
            "--comparison <sha256> is required: a compensation is a reading of one "
            + "recorded measurement, not a fresh calculation.");
        return 2;
    }
    if (!oOpts.TryGetValue("max-axis-spread-pct", out string? strSpread)
        || !double.TryParse(strSpread, System.Globalization.CultureInfo.InvariantCulture, out double fMaxSpread))
    {
        Console.Error.WriteLine(
            "--max-axis-spread-pct is required and takes no default. How much X/Y "
            + "disagreement still permits a single shrinkage factor is a process "
            + "judgement, not a constant this tool may pick for you.");
        return 2;
    }

    try
    {
        CompensationRunResult oResult = CompensationRun.Execute(
            oOpts.GetValueOrDefault("artifacts", "artifacts"),
            oOpts.GetValueOrDefault("ledger", "ledger.db"),
            strComparison,
            fMaxSpread,
            StrGitCommit());

        CompensationProposal oProp = oResult.Proposal;
        Console.WriteLine($"run {oResult.RunId}: {oProp.Verdict}");
        Console.WriteLine($"  {oProp.Reason}");
        if (oProp.Actionable)
        {
            Console.WriteLine(
                $"  nominal XY {oProp.NominalXyMm:F3} mm, measured {oProp.MeasuredXyMm:F3} mm "
                + $"(spread {oProp.AxisSpreadPct:F3} pts)");
            Console.WriteLine(
                "  hand this pair to AdvancedStudio's shrinkage calculator; this tool "
                + "does not compute slicer settings.");
        }
        if (oProp.ZDeviationPct is double fZDev)
            Console.WriteLine($"  z deviated {fZDev:F3} % — reported separately, never folded into XY");
        Console.WriteLine($"  record sha256:{oResult.RecordSha256}");

        if (oOpts.TryGetValue("propose-to-profile", out string? strProfileKey))
        {
            string strConfirmation = CompensationRun.StrPropose(
                oResult,
                oOpts.GetValueOrDefault("studio", "http://localhost:8770").TrimEnd('/'),
                strProfileKey);
            Console.WriteLine(
                $"  proposal {strConfirmation} — awaiting human approval in the studio dashboard");
        }
        return oProp.Actionable ? 0 : 1;
    }
    catch (Exception e) when (e is CompensationException or ArgumentException)
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
            bOffline: aFlags.Contains("offline"),
            strUploadFilename: oOpts.GetValueOrDefault("upload"));

        Console.WriteLine($"handoff {oResult.HandoffId}: {oResult.Status}");
        Console.WriteLine($"  staged  {oResult.StagedStlPath}");
        if (oResult.UploadProposalId.Length > 0)
            Console.WriteLine($"  upload proposal {oResult.UploadProposalId} — awaiting human approval");
        if (oResult.ProposalId.Length > 0)
            Console.WriteLine($"  print proposal  {oResult.ProposalId} — awaiting human approval");
        if (oResult.WillRun.Length > 0)
            Console.WriteLine($"  will run: {oResult.WillRun}");
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
Console.WriteLine("       OpenDesignCore run-cradle --stl <path> --units <mm|cm|m|in|ft> --voxel-mm <v>");
Console.WriteLine("                                 [--clearance-mm <v>] [--wall-mm <v>] [--split <0..1>] [--scale <f>]");
Console.WriteLine("       OpenDesignCore run-calibration-block --instrument-accuracy-mm <v>");
Console.WriteLine("                                            [--x-mm <v>] [--y-mm <v>] [--z-mm <v>] [--voxel-mm <v>]");
Console.WriteLine("       OpenDesignCore compare --design <stl> --units <u> --voxel-mm <v>");
Console.WriteLine("                              (--scan <stl> | --measured <X>x<Y>x<Z>)");
Console.WriteLine("                              [--instrument-accuracy-mm <v>]");
Console.WriteLine("       OpenDesignCore compensate --comparison <sha256> --max-axis-spread-pct <v>");
Console.WriteLine("                                 [--propose-to-profile <key>] [--studio <url>]");
Console.WriteLine("                                 [--artifacts <dir>] [--ledger <path>]");
Console.WriteLine("       OpenDesignCore handoff --run <id> --stage <dir> [--studio <url>]");
Console.WriteLine("                              [--upload <gcode>] [--print <gcode>]");
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
