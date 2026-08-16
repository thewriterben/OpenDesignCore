using System.Globalization;
using OpenDesignCore.Import;
using OpenDesignCore.Provenance;
using OpenDesignCore.Verification;
using PicoGK;

namespace OpenDesignCore.Runs;

public sealed record CompareRunResult
{
    public required long RunId { get; init; }
    public required string DesignSha256 { get; init; }
    public required string ScanSha256 { get; init; }
    public required string ReportSha256 { get; init; }
    public required DimensionalReport Report { get; init; }
}

/// <summary>
/// Closes the loop: design → print → scan → measured deviation, recorded.
///
/// The result is a comparison record, not an artifact — nothing new is
/// manufactured. It still earns a ledger row, because the point of measuring
/// is to be able to say later *which* print, from *which* design, deviated by
/// how much, and a measurement nobody can trace back is an anecdote.
/// </summary>
public static class CompareRun
{
    public const string StrModelId = "dimensional-compare/0.1";

    public static CompareRunResult Execute(
        string strDesignStlPath,
        string strScanStlPath,
        Mesh.EStlUnit eUnits,
        float fVoxelSizeMm,
        string strArtifactsDir,
        string strLedgerPath,
        string strCommit,
        float fScanAccuracyMm = 0f)
    {
        DimensionalReport oReport;
        string strDesignHash, strScanHash;

        using (Library oLib = new(fVoxelSizeMm))
        {
            // Both sides go through the same strict import boundary: units
            // declared, never inferred, and each input content-addressed
            // before it is touched.
            ScanImportResult oDesign = ScanImport.OImport(
                oLib, strDesignStlPath, eUnits, 1.0f, strArtifactsDir);
            ScanImportResult oScan = ScanImport.OImport(
                oLib, strScanStlPath, eUnits, 1.0f, strArtifactsDir);

            strDesignHash = oDesign.ScanSha256;
            strScanHash = oScan.ScanSha256;
            oReport = DimensionalCompare.Compare(
                oDesign.Mesh, oScan.Mesh, fVoxelSizeMm, fScanAccuracyMm);
        }

        Dictionary<string, object?> oRecord = new()
        {
            ["schema"] = "odc/comparison/0.1",
            ["model"] = StrModelId,
            ["voxel_size_mm"] = StrF3(fVoxelSizeMm),
            ["inputs"] = new Dictionary<string, object?>
            {
                ["design_sha256"] = strDesignHash,
                ["scan_sha256"] = strScanHash,
                ["declared_units"] = eUnits.ToString().ToLowerInvariant(),
                ["declared_scan_accuracy_mm"] =
                    fScanAccuracyMm > 0 ? StrF3(fScanAccuracyMm) : "undeclared",
            },
            ["axes"] = oReport.Axes.Select(a => (object?)new Dictionary<string, object?>
            {
                ["axis"] = a.Axis,
                ["design_mm"] = StrF3(a.DesignMm),
                ["scan_mm"] = StrF3(a.ScanMm),
                ["deviation_mm"] = StrF3(a.DeviationMm),
                ["deviation_pct"] = StrF3(a.DeviationPct),
            }).ToList(),
            ["summary"] = new Dictionary<string, object?>
            {
                ["max_abs_deviation_mm"] = StrF3(oReport.MaxAbsDeviationMm),
                ["mean_deviation_pct"] = StrF3(oReport.MeanDeviationPct),
                ["deviation_pct_spread"] = StrF3(oReport.DeviationPctSpread),
                ["within_scan_accuracy"] = oReport.WithinScanAccuracy is bool b
                    ? b : "unknown — no scanner accuracy declared",
                ["design_volume_cubic_mm"] = StrF3(oReport.DesignVolumeCubicMm),
                ["scan_volume_cubic_mm"] = StrF3(oReport.ScanVolumeCubicMm),
            },
            ["caveats"] = new List<object?>
            {
                "A scan's absolute scale is only as good as the scanner's calibration; "
                    + "an apparent deviation may be the scanner, not the print.",
                "Bounding extents are orientation-sensitive. A part scanned rotated "
                    + "relative to its design compares different dimensions and the "
                    + "numbers are meaningless. This is not detected here.",
                "Bulk measurement only — extents and volume. Surface deviation needs "
                    + "point-cloud registration, which is not attempted.",
            },
            ["versions"] = new Dictionary<string, object?>
            {
                ["tool"] = EnclosureRun.StrToolVersion,
                ["picogk"] = EnclosureRun.StrPicoGKVersion,
            },
            ["commit"] = strCommit,
        };

        byte[] abRecord = CanonicalJson.Serialize(oRecord);
        string strReportHash = ArtifactStore.StrStore(strArtifactsDir, abRecord, ".comparison.json");

        using Ledger oLedger = new(strLedgerPath);
        long nRunId = oLedger.NAppend(new RunRecord
        {
            CreatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Model = StrModelId,
            VoxelSizeMm = StrF3(fVoxelSizeMm),
            InputsJson = System.Text.Encoding.ASCII.GetString(
                CanonicalJson.Serialize(oRecord["inputs"])),
            VersionsJson = System.Text.Encoding.ASCII.GetString(
                CanonicalJson.Serialize(oRecord["versions"])),
            ArtifactSha256 = strReportHash,
            ProvenanceSha256 = strReportHash,
            // A comparison "passes" only when a declared scanner accuracy
            // covers the deviation — i.e. the part matches within what the
            // instrument can resolve. With no accuracy declared there is no
            // basis to claim agreement, so it is not a pass. This is a
            // statement about the measurement, never a verdict on the part.
            Passed = oReport.WithinScanAccuracy == true,
        });

        return new CompareRunResult
        {
            RunId = nRunId,
            DesignSha256 = strDesignHash,
            ScanSha256 = strScanHash,
            ReportSha256 = strReportHash,
            Report = oReport,
        };
    }

    private static string StrF3(double fValue) => fValue.ToString("F3", CultureInfo.InvariantCulture);
}
