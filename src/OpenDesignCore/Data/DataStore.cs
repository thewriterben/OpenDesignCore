using System.Text.Json;

namespace OpenDesignCore.Data;

/// <summary>Thrown when reference data fails validation. Message lists every failure.</summary>
public sealed class DataValidationException : Exception
{
    public DataValidationException(IReadOnlyList<string> errors)
        : base("Reference data validation failed:\n  " + string.Join("\n  ", errors))
        => Errors = errors;

    public IReadOnlyList<string> Errors { get; }
}

/// <summary>
/// Loads the git-tracked reference data store (ADR-0006). Strict by design:
/// unknown fields, missing required fields, uncited values, and non-positive
/// dimensions all fail loudly with the file path in the message.
/// </summary>
public static class DataStore
{
    private static readonly JsonSerializerOptions s_oJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
    };

    public static DataSet LoadAll(string strDataDir)
    {
        if (!Directory.Exists(strDataDir))
            throw new DataValidationException([$"data directory not found: {strDataDir}"]);

        List<string> aErrors = [];
        List<PartEntry> aParts = LoadNamespace<PartEntry>(strDataDir, "parts", aErrors);
        List<MaterialEntry> aMaterials = LoadNamespace<MaterialEntry>(strDataDir, "materials", aErrors);

        foreach (PartEntry oPart in aParts)
            ValidatePart(oPart, aErrors);
        foreach (MaterialEntry oMaterial in aMaterials)
            ValidateMaterial(oMaterial, aErrors);

        if (aErrors.Count > 0)
            throw new DataValidationException(aErrors);

        return new DataSet { Parts = aParts, Materials = aMaterials };
    }

    private static List<T> LoadNamespace<T>(string strDataDir, string strNamespace, List<string> aErrors)
    {
        List<T> aEntries = [];
        string strDir = Path.Combine(strDataDir, strNamespace);
        if (!Directory.Exists(strDir))
            return aEntries;

        foreach (string strFile in Directory.EnumerateFiles(strDir, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                T? oEntry = JsonSerializer.Deserialize<T>(File.ReadAllText(strFile), s_oJsonOptions);
                if (oEntry is null)
                    aErrors.Add($"{Rel(strFile)}: null entry");
                else
                    aEntries.Add(oEntry);
            }
            catch (JsonException e)
            {
                aErrors.Add($"{Rel(strFile)}: {e.Message}");
            }
        }
        return aEntries;

        string Rel(string strFile) => Path.Combine(strNamespace, Path.GetFileName(strFile));
    }

    private static void ValidateCommon(string strId, string strNamespace, SourceCitation oSource, List<string> aErrors)
    {
        if (!strId.StartsWith(strNamespace + "/", StringComparison.Ordinal))
            aErrors.Add($"{strId}: id must start with '{strNamespace}/'");
        if (string.IsNullOrWhiteSpace(oSource.Citation))
            aErrors.Add($"{strId}: empty citation — uncited values are invalid (ADR-0006)");
    }

    private static void ValidatePart(PartEntry oPart, List<string> aErrors)
    {
        ValidateCommon(oPart.Id, "parts", oPart.Source, aErrors);
        if (oPart.EnvelopeMm.X <= 0 || oPart.EnvelopeMm.Y <= 0 || oPart.EnvelopeMm.Z <= 0)
            aErrors.Add($"{oPart.Id}: envelope_mm dimensions must be positive");
        if (oPart.ToleranceMm is <= 0)
            aErrors.Add($"{oPart.Id}: tolerance_mm must be positive when present");
    }

    private static void ValidateMaterial(MaterialEntry oMaterial, List<string> aErrors)
    {
        ValidateCommon(oMaterial.Id, "materials", oMaterial.Source, aErrors);
        if (oMaterial.ShrinkagePctRange is { } aRange
            && (aRange.Length != 2 || aRange[0] < 0 || aRange[1] < aRange[0]))
        {
            aErrors.Add($"{oMaterial.Id}: shrinkage_pct_range must be [min, max] with 0 <= min <= max");
        }
        if (oMaterial.FdmToleranceMm is <= 0)
            aErrors.Add($"{oMaterial.Id}: fdm_tolerance_mm must be positive when present");
    }
}
