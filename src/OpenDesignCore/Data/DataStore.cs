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
/// Loads the git-tracked reference data store (ADR-0006) — materials from
/// this repo's <c>data/</c>, parts from the OpenPartsCore registry
/// (ADR-0016, via <see cref="PartsRegistry"/>). Strict by design for data
/// this repo owns: unknown fields, missing required fields, uncited values,
/// and non-positive dimensions all fail loudly with the file path in the message.
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

    /// <param name="strDataDir">This repo's <c>data/</c> (materials).</param>
    /// <param name="strPartsRegistryDir">An OpenPartsCore checkout (parts). See <see cref="PartsRegistry.StrResolveDir"/>.</param>
    public static DataSet LoadAll(string strDataDir, string strPartsRegistryDir)
    {
        if (!Directory.Exists(strDataDir))
            throw new DataValidationException([$"data directory not found: {strDataDir}"]);

        List<string> aErrors = [];
        if (Directory.Exists(Path.Combine(strDataDir, "parts")))
        {
            aErrors.Add(
                $"{Path.Combine(strDataDir, "parts")} exists, but parts are read from OpenPartsCore " +
                "and a private copy would fork the registry (ADR-0016). Delete it.");
        }

        PartsRegistry.Loaded oRegistry = PartsRegistry.Load(strPartsRegistryDir, aErrors);
        List<MaterialEntry> aMaterials = LoadNamespace<MaterialEntry>(strDataDir, "materials", aErrors);

        foreach (MaterialEntry oMaterial in aMaterials)
            ValidateMaterial(oMaterial, aErrors);

        if (aErrors.Count > 0)
            throw new DataValidationException(aErrors);

        return new DataSet
        {
            Parts = oRegistry.Parts,
            UnofferedParts = oRegistry.Unoffered,
            Materials = aMaterials,
            PartsRegistryDir = Path.GetFullPath(strPartsRegistryDir),
            PartsRegistryCommit = oRegistry.Commit,
        };
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
        if (oMaterial.FilamentRef is { } oRef)
        {
            foreach (string strError in oRef.AValidate())
                aErrors.Add($"{oMaterial.Id}: filament_ref {strError}");
        }
    }
}
