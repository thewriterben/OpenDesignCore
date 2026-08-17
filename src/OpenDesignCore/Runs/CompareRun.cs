using System.Globalization;
using System.Numerics;
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

    /// <summary>
    /// Compare a design against dimensions someone measured by hand.
    ///
    /// A caliper is a measuring instrument with a stated accuracy, exactly
    /// like a scanner, so everything downstream is unchanged: significance is
    /// judged against the declared accuracy, axis spread still decides whether
    /// one factor is defensible, and `compensate` applies the same three
    /// refusals. Only the source of the three numbers differs.
    ///
    /// This exists because it makes the loop closable with a caliper instead
    /// of a scanner — and because for a rectangular block, calipers are the
    /// *better* instrument: a hand measurement across two flat faces has no
    /// registration error, no mesh reconstruction, and an accuracy printed on
    /// the tool.
    ///
    /// Volume is not measured. A caliper cannot give one, and inventing it
    /// from the extents would report a solid block's volume for a part that
    /// is mostly infill. The comparison record carries the design's volume
    /// and states the measured volume as absent rather than equal.
    /// </summary>
    public static CompareRunResult ExecuteMeasured(
        string strDesignStlPath,
        Mesh.EStlUnit eUnits,
        float fVoxelSizeMm,
        float fMeasuredXMm,
        float fMeasuredYMm,
        float fMeasuredZMm,
        float fInstrumentAccuracyMm,
        string strArtifactsDir,
        string strLedgerPath,
        string strCommit)
    {
        if (fInstrumentAccuracyMm <= 0)
        {
            throw new ArgumentException(
                "--instrument-accuracy-mm is required and takes no default. Without a stated "
                + "instrument accuracy a deviation cannot be told from instrument error, and "
                + "the comparison would be recorded as unresolvable anyway.");
        }
        if (fMeasuredXMm <= 0 || fMeasuredYMm <= 0 || fMeasuredZMm <= 0)
            throw new ArgumentException("Measured dimensions must all be > 0.");

        DimensionalReport oReport;
        string strDesignHash;

        using (Library oLib = new(fVoxelSizeMm))
        {
            ScanImportResult oDesign = ScanImport.OImport(
                oLib, strDesignStlPath, eUnits, 1.0f, strArtifactsDir);
            strDesignHash = oDesign.ScanSha256;

            Vector3 vecDesign = oDesign.Mesh.oBoundingBox().vecSize();
            Voxels voxDesign = new(oDesign.Mesh);
            voxDesign.CalculateProperties(out float fDesignVolume, out BBox3 _);

            oReport = new DimensionalReport
            {
                Axes =
                [
                    new() { Axis = "x", DesignMm = vecDesign.X, ScanMm = fMeasuredXMm },
                    new() { Axis = "y", DesignMm = vecDesign.Y, ScanMm = fMeasuredYMm },
                    new() { Axis = "z", DesignMm = vecDesign.Z, ScanMm = fMeasuredZMm },
                ],
                DesignVolumeCubicMm = fDesignVolume,
                // Not measured, and deliberately not inferred from the extents:
                // a printed part is not solid.
                ScanVolumeCubicMm = 0,
                VoxelSizeMm = fVoxelSizeMm,
                ScanAccuracyMm = fInstrumentAccuracyMm,
            };
        }

        return ORecord(
            oReport, strDesignHash, strMeasuredBy: "manual",
            strScanHash: "", eUnits: eUnits, fVoxelSizeMm: fVoxelSizeMm,
            fAccuracyMm: fInstrumentAccuracyMm,
            strArtifactsDir: strArtifactsDir, strLedgerPath: strLedgerPath,
            strCommit: strCommit);
    }

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

        return ORecord(
            oReport, strDesignHash, strMeasuredBy: "scan",
            strScanHash: strScanHash, eUnits: eUnits, fVoxelSizeMm: fVoxelSizeMm,
            fAccuracyMm: fScanAccuracyMm,
            strArtifactsDir: strArtifactsDir, strLedgerPath: strLedgerPath,
            strCommit: strCommit);
    }

    /// <summary>
    /// Write the comparison record and ledger row.
    ///
    /// Shared by both measurement paths on purpose: a scan and a caliper
    /// produce the same kind of finding and must produce the same kind of
    /// record, or `compensate` would need to know which instrument was used
    /// and the schema would fork.
    /// </summary>
    private static CompareRunResult ORecord(
        DimensionalReport oReport,
        string strDesignHash,
        string strMeasuredBy,
        string strScanHash,
        Mesh.EStlUnit eUnits,
        float fVoxelSizeMm,
        float fAccuracyMm,
        string strArtifactsDir,
        string strLedgerPath,
        string strCommit)
    {
        Dictionary<string, object?> oRecord = new()
        {
            ["schema"] = "odc/comparison/0.1",
            ["model"] = StrModelId,
            ["voxel_size_mm"] = StrF3(fVoxelSizeMm),
            ["inputs"] = new Dictionary<string, object?>
            {
                ["design_sha256"] = strDesignHash,
                // "manual" carries no scan bytes to hash. Recorded as absent
                // rather than omitted, so a reader can tell a hand measurement
                // from a scan whose hash went missing.
                ["scan_sha256"] = strScanHash.Length > 0 ? strScanHash : "none-measured-by-hand",
                ["measured_by"] = strMeasuredBy,
                ["declared_units"] = eUnits.ToString().ToLowerInvariant(),
                ["declared_scan_accuracy_mm"] =
                    fAccuracyMm > 0 ? StrF3(fAccuracyMm) : "undeclared",
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
                // A caliper cannot give a volume, and deriving one from the
                // extents would report a solid block's volume for a part that
                // is mostly infill. Absent, never inferred.
                ["scan_volume_cubic_mm"] = strMeasuredBy == "manual"
                    ? "not-measurable-by-hand" : StrF3(oReport.ScanVolumeCubicMm),
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

    /// <summary>Parse a measured "XxYxZ" triple in mm.</summary>
    public static (float fX, float fY, float fZ) OParseMeasured(string strText)
    {
        string[] aParts = strText.Replace(" ", "").Split('x', StringSplitOptions.RemoveEmptyEntries);
        if (aParts.Length != 3)
            throw new ArgumentException($"--measured must be XxYxZ in mm, got '{strText}'.");

        float[] aValues = new float[3];
        for (int i = 0; i < 3; i++)
        {
            if (!float.TryParse(aParts[i], CultureInfo.InvariantCulture, out aValues[i]))
                throw new ArgumentException($"--measured must be numeric, got '{strText}'.");
        }
        return (aValues[0], aValues[1], aValues[2]);
    }

    private static string StrF3(double fValue) => fValue.ToString("F3", CultureInfo.InvariantCulture);
}
