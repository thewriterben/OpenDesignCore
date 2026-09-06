using System.Security.Cryptography;
using System.Text.Json;

namespace OpenDesignCore.Data;

/// <summary>
/// Reads parts from an OpenPartsCore checkout (ADR-0016): consume the
/// platform's registry, don't fork it. Only entries carrying a cited
/// <c>envelope_mm</c> (OpenPartsCore ADR-0006) are offered; every other entry
/// is returned by id with the reason it is not, so "not offered" is visible
/// rather than silent.
///
/// The registry's own schema is validated upstream by its CI. This reader is
/// strict about the one object it consumes — the envelope — and deliberately
/// lenient about the rest of the entry, so a new upstream field never breaks
/// this engine (the reverse of <see cref="DataStore"/>'s policy for data this
/// repo owns).
/// </summary>
public static class PartsRegistry
{
    public const string StrEnvVar = "ODC_OPENPARTSCORE";
    public const string StrSiblingDirName = "OpenPartsCore";

    private static readonly string[] s_aNamespaces = ["boards", "electronic", "mechanical"];

    /// <summary>
    /// Where the registry is: <c>ODC_OPENPARTSCORE</c> if set, else the
    /// sibling checkout next to <paramref name="strRepoRoot"/>. The result may
    /// not exist; <see cref="Load"/> says so loudly.
    /// </summary>
    public static string StrResolveDir(string strRepoRoot)
        => Environment.GetEnvironmentVariable(StrEnvVar)
            ?? Path.GetFullPath(Path.Combine(strRepoRoot, "..", StrSiblingDirName));

    public sealed record Loaded
    {
        public required IReadOnlyList<PartEntry> Parts { get; init; }
        public required IReadOnlyList<UnofferedPart> Unoffered { get; init; }
        public required string Commit { get; init; }
    }

    public static Loaded Load(string strRegistryDir, List<string> aErrors)
    {
        string strDataDir = Path.Combine(strRegistryDir, "data");
        if (!Directory.Exists(strDataDir))
        {
            aErrors.Add(
                $"OpenPartsCore registry not found at {strRegistryDir} (no data/ directory). " +
                $"Check out github.com/thewriterben/OpenPartsCore beside this repo or set {StrEnvVar}.");
            return new Loaded { Parts = [], Unoffered = [], Commit = "unresolved" };
        }

        List<PartEntry> aParts = [];
        List<UnofferedPart> aUnoffered = [];

        foreach (string strNamespace in s_aNamespaces)
        {
            string strDir = Path.Combine(strDataDir, strNamespace);
            if (!Directory.Exists(strDir))
                continue;

            foreach (string strFile in Directory.EnumerateFiles(strDir, "*.json").Order(StringComparer.Ordinal))
            {
                string strRel = strNamespace + "/" + Path.GetFileName(strFile);
                byte[] abFile = File.ReadAllBytes(strFile);
                JsonDocument oDoc;
                try
                {
                    oDoc = JsonDocument.Parse(abFile);
                }
                catch (JsonException e)
                {
                    aErrors.Add($"{strRel}: {e.Message}");
                    continue;
                }

                using (oDoc)
                {
                    JsonElement oRoot = oDoc.RootElement;
                    if (!oRoot.TryGetProperty("id", out JsonElement oId) || oId.ValueKind != JsonValueKind.String)
                    {
                        aErrors.Add($"{strRel}: entry has no string 'id'");
                        continue;
                    }
                    string strId = oId.GetString()!;
                    if (!strId.StartsWith(strNamespace + "/", StringComparison.Ordinal))
                        aErrors.Add($"{strRel}: id '{strId}' does not start with '{strNamespace}/'");

                    if (!oRoot.TryGetProperty("envelope_mm", out JsonElement oEnv))
                    {
                        aUnoffered.Add(new UnofferedPart
                        {
                            Id = strId,
                            Reason = "no envelope_mm: the registry has no cited dimensions for it (absent means unknown)",
                        });
                        continue;
                    }

                    PartEntry? oPart = OParse(strId, strRel, oRoot, oEnv, abFile, aErrors);
                    if (oPart is not null)
                        aParts.Add(oPart);
                }
            }
        }

        return new Loaded
        {
            Parts = aParts,
            Unoffered = aUnoffered,
            Commit = GitHead.StrResolve(strRegistryDir) ?? "unresolved",
        };
    }

    private static PartEntry? OParse(
        string strId, string strRel, JsonElement oRoot, JsonElement oEnv, byte[] abFile, List<string> aErrors)
    {
        int nBefore = aErrors.Count;

        if (oEnv.ValueKind != JsonValueKind.Object)
        {
            aErrors.Add($"{strId}: envelope_mm must be an object");
            return null;
        }

        foreach (JsonProperty oProp in oEnv.EnumerateObject())
        {
            if (oProp.Name is not ("x" or "y" or "z" or "tolerance_mm" or "source"))
                aErrors.Add($"{strId}: envelope_mm has unknown key '{oProp.Name}'");
        }

        double FAxis(string strAxis)
        {
            if (!oEnv.TryGetProperty(strAxis, out JsonElement o) || o.ValueKind != JsonValueKind.Number)
            {
                aErrors.Add($"{strId}: envelope_mm.{strAxis} missing or not a number — absent means unknown, never zero");
                return double.NaN;
            }
            double f = o.GetDouble();
            if (f <= 0)
                aErrors.Add($"{strId}: envelope_mm.{strAxis} must be positive");
            return f;
        }

        double fX = FAxis("x"), fY = FAxis("y"), fZ = FAxis("z");

        double? fTolerance = null;
        if (oEnv.TryGetProperty("tolerance_mm", out JsonElement oTol))
        {
            if (oTol.ValueKind != JsonValueKind.Number || oTol.GetDouble() <= 0)
                aErrors.Add($"{strId}: envelope_mm.tolerance_mm must be a positive number when present");
            else
                fTolerance = oTol.GetDouble();
        }

        string strCitation = "";
        string? strUrl = null, strRetrieved = null;
        if (oEnv.TryGetProperty("source", out JsonElement oSource) && oSource.ValueKind == JsonValueKind.Object)
        {
            strCitation = oSource.TryGetProperty("citation", out JsonElement oCit) && oCit.ValueKind == JsonValueKind.String
                ? oCit.GetString()! : "";
            strUrl = oSource.TryGetProperty("url", out JsonElement oUrl) && oUrl.ValueKind == JsonValueKind.String
                ? oUrl.GetString() : null;
            strRetrieved = oSource.TryGetProperty("retrieved", out JsonElement oRet) && oRet.ValueKind == JsonValueKind.String
                ? oRet.GetString() : null;
        }
        if (string.IsNullOrWhiteSpace(strCitation))
        {
            aErrors.Add(
                $"{strId}: envelope_mm has no citation of its own — the entry-level source does not cover it " +
                "(OpenPartsCore ADR-0006; uncited values are invalid, ADR-0006)");
        }

        if (aErrors.Count > nBefore)
            return null;

        string strName = oRoot.TryGetProperty("name", out JsonElement oName) && oName.ValueKind == JsonValueKind.String
            ? oName.GetString()! : strId;
        string? strDescription = oRoot.TryGetProperty("description", out JsonElement oDesc) && oDesc.ValueKind == JsonValueKind.String
            ? oDesc.GetString() : null;

        return new PartEntry
        {
            Id = strId,
            Name = strName,
            Description = strDescription,
            EnvelopeMm = new EnvelopeMm { X = fX, Y = fY, Z = fZ },
            ToleranceMm = fTolerance,
            Source = new SourceCitation { Citation = strCitation, Url = strUrl, Retrieved = strRetrieved },
            FileSha256 = Convert.ToHexStringLower(SHA256.HashData(abFile)),
        };
    }
}

/// <summary>
/// Resolves a checkout's HEAD commit by reading <c>.git</c> directly, so
/// provenance does not depend on a git executable being on PATH. Handles a
/// detached HEAD, a symbolic ref under <c>refs/</c>, packed refs, and a
/// worktree's <c>.git</c> file. Returns null when none of those apply.
/// </summary>
public static class GitHead
{
    public static string? StrResolve(string strRepoDir)
    {
        try
        {
            string strGit = Path.Combine(strRepoDir, ".git");
            if (File.Exists(strGit))
            {
                // Worktree or submodule: ".git" is a file pointing at the real dir.
                string strLine = File.ReadAllText(strGit).Trim();
                const string strPrefix = "gitdir: ";
                if (!strLine.StartsWith(strPrefix, StringComparison.Ordinal))
                    return null;
                strGit = Path.GetFullPath(Path.Combine(strRepoDir, strLine[strPrefix.Length..]));
            }
            if (!Directory.Exists(strGit))
                return null;

            string strHead = File.ReadAllText(Path.Combine(strGit, "HEAD")).Trim();
            if (!strHead.StartsWith("ref: ", StringComparison.Ordinal))
                return StrIfSha(strHead);

            string strRef = strHead[5..];
            // A worktree's HEAD lives in its own gitdir but refs live in the common dir.
            string strCommon = strGit;
            string strCommonFile = Path.Combine(strGit, "commondir");
            if (File.Exists(strCommonFile))
                strCommon = Path.GetFullPath(Path.Combine(strGit, File.ReadAllText(strCommonFile).Trim()));

            string strLoose = Path.Combine(strCommon, strRef.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(strLoose))
                return StrIfSha(File.ReadAllText(strLoose).Trim());

            string strPacked = Path.Combine(strCommon, "packed-refs");
            if (File.Exists(strPacked))
            {
                foreach (string strLine in File.ReadLines(strPacked))
                {
                    if (strLine.Length > 41 && strLine[40] == ' ' && strLine[41..].Trim() == strRef)
                        return StrIfSha(strLine[..40]);
                }
            }
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? StrIfSha(string s)
        => s.Length == 40 && s.All(Uri.IsHexDigit) ? s.ToLowerInvariant() : null;
}
