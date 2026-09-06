using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// A minimal OpenPartsCore checkout written to a temp directory, so the
/// registry reader (ADR-0016) is exercised without depending on the sibling
/// repo being present. Values are synthetic and say so in their citation —
/// a fixture is not a source of physical data.
/// </summary>
internal static class TestRegistry
{
    public const string StrFixtureId = "electronic/fixture-module";
    public const string StrUnofferedId = "boards/fixture-board-no-envelope";

    /// <summary>Envelope of the fixture module. Synthetic; chosen so the tray is a few hundred voxels at 0.5 mm.</summary>
    public const double FX = 12.0, FY = 20.0, FZ = 4.0;

    public static string StrWrite(string strDir)
    {
        Directory.CreateDirectory(Path.Combine(strDir, "data", "electronic"));
        Directory.CreateDirectory(Path.Combine(strDir, "data", "boards"));

        File.WriteAllText(Path.Combine(strDir, "data", "electronic", "fixture-module.json"), $$"""
            {
              "schema_version": 0,
              "id": "{{StrFixtureId}}",
              "namespace": "electronic",
              "name": "fixture-module",
              "description": "Synthetic test fixture",
              "source": { "citation": "test fixture — not a real part" },
              "attributes": { "vendor": "none", "some_new_upstream_field": true },
              "envelope_mm": {
                "x": {{FX}}, "y": {{FY}}, "z": {{FZ}},
                "tolerance_mm": 0.1,
                "source": { "citation": "test fixture envelope — synthetic values", "retrieved": "2026-09-06" }
              },
              "links": {}
            }
            """);

        File.WriteAllText(Path.Combine(strDir, "data", "boards", "fixture-board-no-envelope.json"), $$"""
            {
              "schema_version": 0,
              "id": "{{StrUnofferedId}}",
              "namespace": "boards",
              "name": "fixture-board-no-envelope",
              "source": { "citation": "test fixture — a registry entry with no dimensions" },
              "attributes": { "usb_ids": [] },
              "links": {}
            }
            """);

        return strDir;
    }

    /// <summary>Write one extra entry into an existing fixture registry.</summary>
    public static void StrWriteEntry(string strDir, string strNamespace, string strFileName, string strJson)
    {
        string strNs = Path.Combine(strDir, "data", strNamespace);
        Directory.CreateDirectory(strNs);
        File.WriteAllText(Path.Combine(strNs, strFileName), strJson);
    }

    /// <summary>Walk up from the test binary to the repository root.</summary>
    public static string StrRepoRoot()
    {
        DirectoryInfo? oDir = new(AppContext.BaseDirectory);
        while (oDir is not null && !File.Exists(Path.Combine(oDir.FullName, "OpenDesignCore.sln")))
            oDir = oDir.Parent;
        Assert.NotNull(oDir);
        return oDir.FullName;
    }

    /// <summary>
    /// The real sibling checkout, or null when it is not there. Tests that
    /// read it return early in that case — the repo's convention for
    /// cross-repo checks (see MachineCalibrationTests) — because CI for this
    /// repo does not clone its peers.
    /// </summary>
    public static string? StrSiblingRegistryOrNull()
    {
        // Same resolution the engine uses: the env var (which is how CI
        // supplies a checkout that cannot be a sibling), else ../OpenPartsCore.
        string strDir = OpenDesignCore.Data.PartsRegistry.StrResolveDir(StrRepoRoot());
        return Directory.Exists(Path.Combine(strDir, "data")) ? strDir : null;
    }
}
