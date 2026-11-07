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

    /// <summary>
    /// The <c>features</c> block of one entry (OpenPartsCore ADR-0007), or null
    /// with the reason in <paramref name="aErrors"/>. Strict, like the
    /// envelope: this engine places geometry against these numbers, so an
    /// unknown key or a malformed item is an error rather than something to
    /// skip. An entry without <c>features</c> is an error too — the caller
    /// asked for a clearance check, and "nothing described" is not "clear".
    /// </summary>
    public static PartFeatures? OLoadFeatures(string strRegistryDir, string strId, List<string> aErrors)
    {
        int nSlash = strId.IndexOf('/');
        string strFile = nSlash < 0 ? "" : Path.Combine(
            strRegistryDir, "data", strId[..nSlash], strId[(nSlash + 1)..] + ".json");
        if (!File.Exists(strFile))
        {
            aErrors.Add($"{strId}: no registry file at {strFile}. Check out OpenPartsCore or set {StrEnvVar}.");
            return null;
        }
        byte[] abFile = File.ReadAllBytes(strFile);
        using JsonDocument oDoc = JsonDocument.Parse(abFile);
        JsonElement oRoot = oDoc.RootElement;
        if (!oRoot.TryGetProperty("id", out JsonElement oId) || oId.GetString() != strId)
        {
            aErrors.Add($"{strFile}: id does not match '{strId}'");
            return null;
        }
        if (!oRoot.TryGetProperty("features", out JsonElement oFeat) || oFeat.ValueKind != JsonValueKind.Object)
        {
            aErrors.Add(
                $"{strId}: the registry has no features for this part (OpenPartsCore ADR-0007). "
                + "A clearance check against nothing would pass everything; refusing.");
            return null;
        }

        int nBefore = aErrors.Count;
        void Err(string s) => aErrors.Add($"{strId}: features{s}");

        foreach (JsonProperty o in oFeat.EnumerateObject())
            if (o.Name is not ("source" or "frame" or "not_modelled" or "items"))
                Err($" has unknown key '{o.Name}'");

        string StrOf(JsonElement o, string strKey, string strWhere)
        {
            if (o.TryGetProperty(strKey, out JsonElement v) && v.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(v.GetString()))
                return v.GetString()!;
            Err($"{strWhere}.{strKey} missing or empty");
            return "";
        }

        string strCitation = "", strSha = "", strRevision = "";
        if (oFeat.TryGetProperty("source", out JsonElement oSrc) && oSrc.ValueKind == JsonValueKind.Object)
        {
            strCitation = StrOf(oSrc, "citation", ".source");
            strSha = StrOf(oSrc, "model_sha256", ".source");
            strRevision = StrOf(oSrc, "revision", ".source");
        }
        else
        {
            Err(".source missing - features carry their own source");
        }
        string strFrame = StrOf(oFeat, "frame", "");

        List<string> aNotModelled = [];
        if (oFeat.TryGetProperty("not_modelled", out JsonElement oNm) && oNm.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement o in oNm.EnumerateArray())
                aNotModelled.Add(o.GetString() ?? "");
        }
        else
        {
            Err(".not_modelled missing - say what the items leave out, even if nothing");
        }

        List<PartFeature> aItems = [];
        HashSet<string> oSeen = new(StringComparer.Ordinal);
        if (oFeat.TryGetProperty("items", out JsonElement oItems) && oItems.ValueKind == JsonValueKind.Array)
        {
            int i = 0;
            foreach (JsonElement oItem in oItems.EnumerateArray())
            {
                PartFeature? oF = OParseFeature(oItem, $".items[{i++}]", Err);
                if (oF is not null && !oSeen.Add(oF.Id))
                    Err($".items: duplicate id '{oF.Id}'");
                else if (oF is not null)
                    aItems.Add(oF);
            }
        }
        if (aItems.Count == 0)
            Err(".items missing or empty");

        if (aErrors.Count > nBefore)
            return null;

        return new PartFeatures
        {
            PartId = strId,
            Citation = strCitation,
            ModelSha256 = strSha,
            Revision = strRevision,
            Frame = strFrame,
            NotModelled = aNotModelled,
            Items = aItems,
            FileSha256 = Convert.ToHexStringLower(SHA256.HashData(abFile)),
            RegistryCommit = GitHead.StrResolve(strRegistryDir) ?? "unresolved",
        };
    }

    private static readonly string[] s_aAccessKinds = ["connector", "switch", "card_slot", "antenna", "optic"];
    private static readonly string[] s_aKinds = ["board", "connector", "switch", "card_slot", "antenna", "optic", "component"];

    private static PartFeature? OParseFeature(JsonElement o, string strWhere, Action<string> Err)
    {
        if (o.ValueKind != JsonValueKind.Object)
        {
            Err($"{strWhere} must be an object");
            return null;
        }
        foreach (JsonProperty p in o.EnumerateObject())
            if (p.Name is not ("id" or "kind" or "box_mm" or "cylinder_mm" or "source" or "access"))
                Err($"{strWhere} has unknown key '{p.Name}'");

        string? strId = o.TryGetProperty("id", out JsonElement oId) ? oId.GetString() : null;
        string? strKind = o.TryGetProperty("kind", out JsonElement oKind) ? oKind.GetString() : null;
        string? strSource = o.TryGetProperty("source", out JsonElement oS) ? oS.GetString() : null;
        if (string.IsNullOrWhiteSpace(strId) || strKind is null || !s_aKinds.Contains(strKind)
            || string.IsNullOrWhiteSpace(strSource))
        {
            Err($"{strWhere}: id, a known kind and a source are all required");
            return null;
        }
        strWhere += $" '{strId}'";

        static double[]? AVec3(JsonElement parent, string strKey)
        {
            if (!parent.TryGetProperty(strKey, out JsonElement v) || v.ValueKind != JsonValueKind.Array
                || v.GetArrayLength() != 3 || v.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.Number))
                return null;
            return [.. v.EnumerateArray().Select(e => e.GetDouble())];
        }

        double[]? aMin = null, aMax = null;
        FeatureCylinder? oCyl = null;
        bool bBox = o.TryGetProperty("box_mm", out JsonElement oBox);
        bool bCyl = o.TryGetProperty("cylinder_mm", out JsonElement oC);
        if (bBox == bCyl)
        {
            Err($"{strWhere}: exactly one of box_mm or cylinder_mm");
            return null;
        }
        if (bBox)
        {
            aMin = AVec3(oBox, "min_mm");
            aMax = AVec3(oBox, "max_mm");
            if (aMin is null || aMax is null || Enumerable.Range(0, 3).Any(k => aMin[k] >= aMax[k]))
            {
                Err($"{strWhere}: box_mm needs min_mm below max_mm on every axis");
                return null;
            }
        }
        else
        {
            Axis? eAxis = AxisExt.OParse(oC.TryGetProperty("axis", out JsonElement oA) ? oA.GetString() : null);
            double[]? aBase = AVec3(oC, "base_mm");
            double fLen = oC.TryGetProperty("length_mm", out JsonElement oL) && oL.ValueKind == JsonValueKind.Number ? oL.GetDouble() : 0;
            double fDia = oC.TryGetProperty("diameter_mm", out JsonElement oD) && oD.ValueKind == JsonValueKind.Number ? oD.GetDouble() : 0;
            if (eAxis is null || aBase is null || fLen <= 0 || fDia <= 0)
            {
                Err($"{strWhere}: cylinder_mm needs an axis, base_mm, and positive length_mm and diameter_mm");
                return null;
            }
            oCyl = new FeatureCylinder { Axis = eAxis.Value, BaseMm = aBase, LengthMm = fLen, DiameterMm = fDia };
        }

        FeatureAccess? oAccess = null;
        if (o.TryGetProperty("access", out JsonElement oAcc) && oAcc.ValueKind == JsonValueKind.Object)
        {
            Axis? eDir = null;
            if (oAcc.TryGetProperty("direction", out JsonElement oDir) && oDir.ValueKind != JsonValueKind.Null)
            {
                eDir = AxisExt.OParse(oDir.GetString());
                if (eDir is null)
                    Err($"{strWhere}: access.direction must be ±x/±y/±z or null");
            }
            double? fDepth = null;
            if (oAcc.TryGetProperty("depth_mm", out JsonElement oDep) && oDep.ValueKind != JsonValueKind.Null)
            {
                if (oDep.ValueKind != JsonValueKind.Number || oDep.GetDouble() <= 0)
                    Err($"{strWhere}: access.depth_mm must be positive or null");
                else
                    fDepth = oDep.GetDouble();
            }
            double[]? aSection = null;
            if (oAcc.TryGetProperty("section_mm", out JsonElement oSec))
            {
                if (oSec.ValueKind != JsonValueKind.Array || oSec.GetArrayLength() != 2
                    || oSec.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.Number || e.GetDouble() <= 0))
                    Err($"{strWhere}: access.section_mm must be two positive numbers");
                else
                    aSection = [.. oSec.EnumerateArray().Select(e => e.GetDouble())];
            }
            string strAccSource = oAcc.TryGetProperty("source", out JsonElement oAs) ? oAs.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(strAccSource))
                Err($"{strWhere}: access has no source");
            oAccess = new FeatureAccess { Direction = eDir, DepthMm = fDepth, SectionMm = aSection, Source = strAccSource };
        }
        else if (s_aAccessKinds.Contains(strKind))
        {
            Err($"{strWhere}: a {strKind} must declare access (null direction/depth = unknown)");
        }

        return new PartFeature
        {
            Id = strId,
            Kind = strKind,
            MinMm = aMin,
            MaxMm = aMax,
            Cylinder = oCyl,
            Source = strSource,
            Access = oAccess,
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
        else if ((DataStore.StrSiyuanReference(strCitation) ?? DataStore.StrSiyuanReference(strUrl)) is { } strSiyuan)
        {
            aErrors.Add($"{strId}: envelope_mm citation references SiYuan ({strSiyuan}): a projection is never a source (OpenKnowledgeCore ADR-0001)");
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
