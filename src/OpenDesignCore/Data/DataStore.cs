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

    // Rejection patterns and labels copied from OpenKnowledgeCore okc/validate.py (REJECTIONS),
    // per its ADR-0001 "SiYuan is a projection, never a source". Keep in step with that file.
    private static readonly (string strLabel, System.Text.RegularExpressions.Regex oPattern)[] s_aSiyuanRejections =
    [
        ("siyuan URL", new(@"siyuan://", System.Text.RegularExpressions.RegexOptions.IgnoreCase)),
        ("SiYuan block id", new(@"\b\d{14}-[a-z0-9]{7}\b")),
        ("SiYuan kernel address", new(@"(localhost|127\.0\.0\.1|\[::1\]):6806\b")),
        ("SiYuan .sy file path", new(@"(^|[\\/ ])[^\s\\/]+\.sy(\b|$)")),
    ];

    // Strict citation forms, copied from OpenPartsCore scripts/validate.py (CITATION_FORMS),
    // which follows OpenKnowledgeCore ADR-0001. Keep in step with that file (ADR-0023).
    private static readonly (string strForm, System.Text.RegularExpressions.Regex oPattern)[] s_aCitationForms =
    [
        ("vault", new(@"^vault:sha256:[0-9a-f]{64}(#page=\d+)?$")),
        ("repo", new(@"^repo:[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[0-9a-f]{7,40}:[^\s:][^\s]*$")),
        ("ledger", new(@"^ledger:[a-z0-9_-]+:[A-Za-z0-9_.-]+$")),
        ("doi", new(@"^doi:10\.\d{4,9}/\S+$")),
        ("arxiv", new(@"^arxiv:\d{4}\.\d{4,5}(v\d+)?$")),
        ("standard", new(@"^standard:[A-Z][A-Z0-9/ -]*? [0-9][0-9.-]*:(19|20)\d{2}$")),
    ];

    /// <summary>Namespaces whose citations must take one of the strict forms (ADR-0023).</summary>
    public static readonly IReadOnlySet<string> StrictNamespaces = new HashSet<string>(StringComparer.Ordinal) { "materials" };

    /// <summary>The strict form the citation takes ("vault", "repo", ...), or null when it is free text.</summary>
    public static string? StrCitationForm(string? strCitation)
    {
        if (string.IsNullOrEmpty(strCitation)) return null;
        foreach ((string strForm, System.Text.RegularExpressions.Regex oPattern) in s_aCitationForms)
        {
            if (oPattern.IsMatch(strCitation)) return strForm;
        }
        return null;
    }

    /// <summary>The label of the first SiYuan pattern the text matches, or null when it references none.</summary>
    internal static string? StrSiyuanReference(string? strText)
    {
        if (string.IsNullOrEmpty(strText)) return null;
        foreach ((string strLabel, System.Text.RegularExpressions.Regex oPattern) in s_aSiyuanRejections)
        {
            if (oPattern.IsMatch(strText)) return strLabel;
        }
        return null;
    }

    private static void ValidateCommon(string strId, string strNamespace, SourceCitation oSource, List<string> aErrors)
    {
        if (!strId.StartsWith(strNamespace + "/", StringComparison.Ordinal))
            aErrors.Add($"{strId}: id must start with '{strNamespace}/'");
        if (string.IsNullOrWhiteSpace(oSource.Citation))
            aErrors.Add($"{strId}: empty citation — uncited values are invalid (ADR-0006)");
        else if (StrictNamespaces.Contains(strNamespace) && StrCitationForm(oSource.Citation) is null)
            aErrors.Add($"{strId}: citation is free text; '{strNamespace}' takes only vault:, repo:, ledger:, doi:, arxiv: or standard: forms, with prose in the description (ADR-0023)");
        foreach (string? strText in new[] { oSource.Citation, oSource.Url })
        {
            if (StrSiyuanReference(strText) is { } strLabel)
            {
                aErrors.Add($"{strId}: citation references SiYuan ({strLabel}): a projection is never a source (OpenKnowledgeCore ADR-0001)");
                break;
            }
        }
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
