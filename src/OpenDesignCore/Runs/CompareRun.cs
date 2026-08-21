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
        string strCommit,
        string strMaterial,
        float fMeasuredZLowMm = 0f,
        float fNominalZLowMm = 0f)
    {
        // Compensation is per material and per spool — the ADRs said so from
        // the start, and nothing enforced it. The material was never captured
        // anywhere in the pipeline, so a PLA measurement could be proposed to
        // a PETG profile carrying a provenance hash that made it look
        // rigorous. Declared here, never inferred, like units and accuracy.
        if (string.IsNullOrWhiteSpace(strMaterial))
        {
            throw new ArgumentException(
                "--material is required and takes no default. A shrinkage figure describes "
                + "one material, and without recording which one, a measurement taken on PLA "
                + "can be proposed to a PETG profile with a hash that makes it look sourced.");
        }

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

            // With a shelf reading, the Z axis reports the SPAN between the
            // two top faces rather than either height. Both readings start at
            // the bed and carry the same first-layer squish, so their
            // difference carries none — which is the only way a caliper can
            // give a Z shrinkage that is not contaminated by the first layer.
            bool bHasSpan = fMeasuredZLowMm > 0 && fNominalZLowMm > 0;
            double fZDesign = bHasSpan ? vecDesign.Z - fNominalZLowMm : vecDesign.Z;
            double fZMeasured = bHasSpan ? fMeasuredZMm - fMeasuredZLowMm : fMeasuredZMm;

            oReport = new DimensionalReport
            {
                Axes =
                [
                    new() { Axis = "x", DesignMm = vecDesign.X, ScanMm = fMeasuredXMm },
                    new() { Axis = "y", DesignMm = vecDesign.Y, ScanMm = fMeasuredYMm },
                    new() { Axis = bHasSpan ? "z-span" : "z", DesignMm = fZDesign, ScanMm = fZMeasured },
                ],
                DesignVolumeCubicMm = fDesignVolume,
                // Not measured, and deliberately not inferred from the extents:
                // a printed part is not solid.
                ScanVolumeCubicMm = 0,
                VoxelSizeMm = fVoxelSizeMm,
                ScanAccuracyMm = fInstrumentAccuracyMm,
            };
        }

        double? fFirstLayerOffset = null;
        if (fMeasuredZLowMm > 0 && fNominalZLowMm > 0)
        {
            AxisDeviation oZ = oReport.Axes.Single(a => a.Axis == "z-span");
            double fScale = oZ.ScanMm / oZ.DesignMm;
            fFirstLayerOffset = fMeasuredZLowMm - (fNominalZLowMm * fScale);
        }

        return ORecord(
            oReport, strDesignHash, strMeasuredBy: "manual",
            strScanHash: "", eUnits: eUnits, fVoxelSizeMm: fVoxelSizeMm,
            fAccuracyMm: fInstrumentAccuracyMm,
            strArtifactsDir: strArtifactsDir, strLedgerPath: strLedgerPath,
            strCommit: strCommit, fFirstLayerOffsetMm: fFirstLayerOffset,
            strMaterial: strMaterial.Trim().ToLowerInvariant());
    }

    public static CompareRunResult Execute(
        string strDesignStlPath,
        string strScanStlPath,
        Mesh.EStlUnit eUnits,
        float fVoxelSizeMm,
        string strArtifactsDir,
        string strLedgerPath,
        string strCommit,
        string strMaterial,
        float fScanAccuracyMm = 0f)
    {
        // Required on this path too. The material is a property of the printed
        // part, not of the instrument that measured it, so a scan needs it
        // exactly as much as a caliper does.
        if (string.IsNullOrWhiteSpace(strMaterial))
        {
            throw new ArgumentException(
                "--material is required and takes no default. A shrinkage figure describes "
                + "one material, and an unlabelled measurement is how one material's number "
                + "ends up in another material's profile.");
        }

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
            strCommit: strCommit,
            strMaterial: strMaterial.Trim().ToLowerInvariant());
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
        string strCommit,
        double? fFirstLayerOffsetMm = null,
        string strMaterial = "")
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
                // The material the part was printed in. A shrinkage figure
                // describes one material; without this the pipeline cannot
                // stop a PLA measurement reaching a PETG profile.
                ["material"] = strMaterial.Length > 0 ? strMaterial : "undeclared",
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
                // The constant that the Z span was measured in order to
                // remove. Recorded because it is useful on its own: this is
                // the first-layer / elephant's-foot figure, a different slicer
                // setting from shrinkage, and no percentage will fix it.
                ["first_layer_offset_mm"] = fFirstLayerOffsetMm is double fOff
                    ? StrF3(fOff) : "not-separable-from-a-single-height",
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

    /// <summary>
    /// Parse a measured triple or quadruple in mm.
    ///
    /// Three values are X, Y and a single bed-referenced Z. Four are X, Y and
    /// two heights — the shelf and the tall face — from which a Z scale can be
    /// derived without the first-layer offset, because both readings contain
    /// the same offset and their difference contains none.
    /// </summary>
    public static float[] AParseMeasured(string strText)
    {
        string[] aParts = strText.Replace(" ", "").Split('x', StringSplitOptions.RemoveEmptyEntries);
        if (aParts.Length is not (3 or 4))
        {
            throw new ArgumentException(
                $"--measured takes XxYxZ, or XxYxZlowxZhigh for a stepped block, got '{strText}'.");
        }

        float[] aValues = new float[aParts.Length];
        for (int i = 0; i < aParts.Length; i++)
        {
            if (!float.TryParse(aParts[i], CultureInfo.InvariantCulture, out aValues[i]))
                throw new ArgumentException($"--measured must be numeric, got '{strText}'.");
        }
        return aValues;
    }

    /// <summary>
    /// Separate proportional Z shrinkage from the constant first-layer offset,
    /// given two heights measured on one part.
    ///
    /// Both readings start at the bed, so both contain the same squish. Two
    /// equations, two unknowns:
    ///
    /// <code>
    ///   measured_low  = nominal_low  × scale + offset
    ///   measured_high = nominal_high × scale + offset
    /// </code>
    ///
    /// The scale falls out of the difference, where the offset has cancelled.
    /// The offset then falls out of either equation, and is worth having on
    /// its own: it is the elephant's-foot / first-layer figure, a different
    /// slicer setting from shrinkage and one that no percentage can fix.
    ///
    /// This exists because the earlier advice — "measure Z away from the first
    /// layer" — was not merely hard, it was impossible. A bed-printed part's
    /// height begins at the first layer.
    /// </summary>
    public static (double fScale, double fOffsetMm) OSolveZ(
        double fNominalLowMm, double fNominalHighMm,
        double fMeasuredLowMm, double fMeasuredHighMm)
    {
        double fNominalSpan = fNominalHighMm - fNominalLowMm;
        if (fNominalSpan <= 0)
        {
            throw new ArgumentException(
                "The tall face must be above the shelf, or there is no span to measure and "
                + "the first-layer offset cannot be separated from shrinkage.");
        }

        double fScale = (fMeasuredHighMm - fMeasuredLowMm) / fNominalSpan;
        double fOffset = fMeasuredLowMm - (fNominalLowMm * fScale);
        return (fScale, fOffset);
    }

    private static string StrF3(double fValue) => fValue.ToString("F3", CultureInfo.InvariantCulture);
}
