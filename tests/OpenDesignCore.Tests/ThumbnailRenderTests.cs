using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using OpenDesignCore.Provenance;
using OpenDesignCore.Rendering;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// The thumbnail record and the PNG normaliser, against constructed inputs (CI
/// has no Blender), plus one test against the real thing that returns early when
/// Blender is not installed — the repo's convention for checks that need a
/// neighbour, as in <see cref="BlenderCrossCheckTests"/>.
/// </summary>
[Collection(ProcessEnvironmentCollection.StrName)]
public sealed class ThumbnailRenderTests
{
    // Run 50's artifact as Blender 5.2.1 reports it, measured 2026-09-11.
    private static ThumbnailSettings ORun50Settings() => new()
    {
        BlenderVersion = "5.2.1 LTS",
        Engine = "BLENDER_WORKBENCH",
        ResolutionPx = 512,
        AntiAliasing = "OFF",
        ViewTransform = "Standard",
        Projection = "ORTHO",
        ViewDirection = [1.0, -1.0, 0.7],
        Margin = 1.05,
        OrthoScale = 78.0321,
        ExtentX = 30.90,
        ExtentY = 66.87,
        ExtentZ = 11.15,
        Vertices = 88356,
        Faces = 176708,
    };

    // ---- the record -------------------------------------------------------

    [Fact]
    public void Record_IsCanonicalAndCarriesEverythingASecondRenderNeeds()
    {
        Dictionary<string, object?> o = ThumbnailRender.OBuildRecord(
            50, "artifact-hash", "sidecar-hash", "image-hash", ORun50Settings(),
            "blender.exe", "script-hash", "test-commit");

        byte[] ab1 = CanonicalJson.Serialize(o);
        byte[] ab2 = CanonicalJson.Serialize(o);
        Assert.Equal(ab1, ab2);

        using JsonDocument oDoc = JsonDocument.Parse(ab1);
        JsonElement r = oDoc.RootElement;
        Assert.Equal("odc/thumbnail/0.1", r.GetProperty("schema").GetString());
        Assert.Equal("blender-thumbnail/0.1", r.GetProperty("model").GetString());

        // The three things that decide the pixels: engine, sampling, projection.
        JsonElement s = r.GetProperty("settings");
        Assert.Equal("BLENDER_WORKBENCH", s.GetProperty("engine").GetString());
        Assert.Equal("OFF", s.GetProperty("anti_aliasing").GetString());
        Assert.Equal("ORTHO", s.GetProperty("projection").GetString());
        Assert.Equal("1.050", s.GetProperty("margin").GetString());
        Assert.Equal(["1.000", "-1.000", "0.700"],
            s.GetProperty("view_direction").EnumerateArray().Select(v => v.GetString()));

        Assert.Equal("script-hash", r.GetProperty("inputs").GetProperty("render_script_sha256").GetString());
        Assert.Equal("image-hash", r.GetProperty("image").GetProperty("sha256").GetString());
        Assert.Equal("5.2.1 LTS", r.GetProperty("versions").GetProperty("blender").GetString());
    }

    /// <summary>
    /// ADR-0017 kept thumbnails out because a picture that looks right invites a
    /// conclusion it cannot support. ADR-0022 admits them and answers that by
    /// making every record say so. If this test is ever deleted to make the JSON
    /// tidier, the reason for the whole record type goes with it.
    /// </summary>
    [Fact]
    public void EveryRecord_SaysItIsNotAMeasurement()
    {
        Dictionary<string, object?> o = ThumbnailRender.OBuildRecord(
            50, "a", "b", "c", ORun50Settings(), "blender.exe", "s", "commit");
        using JsonDocument oDoc = JsonDocument.Parse(CanonicalJson.Serialize(o));
        string strCaveats = string.Join(" ",
            oDoc.RootElement.GetProperty("caveats").EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("not a measurement", strCaveats);
        Assert.Contains("verify-artifact", strCaveats);
        Assert.DoesNotContain("passed", oDoc.RootElement.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void RecordCarriesTheMeshItProjected_NotAVerdictOnIt()
    {
        Dictionary<string, object?> o = ThumbnailRender.OBuildRecord(
            50, "a", "b", "c", ORun50Settings(), "blender.exe", "s", "commit");
        using JsonDocument oDoc = JsonDocument.Parse(CanonicalJson.Serialize(o));
        JsonElement m = oDoc.RootElement.GetProperty("mesh");
        Assert.Equal("66.870", m.GetProperty("extent_mm").GetProperty("y").GetString());
        Assert.Equal(176708, m.GetProperty("faces").GetInt32());
    }

    [Fact]
    public void Settings_ParseFromTheScriptsOwnResultLine()
    {
        ThumbnailSettings c = ThumbnailSettings.OParse("""
            {"blender":"5.2.1 LTS","engine":"BLENDER_WORKBENCH","resolution_px":512,
             "anti_aliasing":"OFF","view_transform":"Standard","projection":"ORTHO",
             "view_direction":[1.0,-1.0,0.7],"margin":1.05,"ortho_scale":78.0321,
             "extent_mm":{"x":30.9,"y":66.87,"z":11.15},"vertices":88356,"faces":176708}
            """);
        Assert.Equal(512, c.ResolutionPx);
        Assert.Equal(66.87, c.ExtentY, 9);
        Assert.Equal([1.0, -1.0, 0.7], c.ViewDirection);
    }

    [Fact]
    public void RenderScript_IsEmbeddedAndHashed()
    {
        (string strText, string strSha) = ThumbnailRender.RenderScript();
        Assert.Contains("__ODC_RESULT__", strText);
        Assert.Contains("BLENDER_WORKBENCH", strText);
        Assert.Equal(64, strSha.Length);
    }

    // ---- the normaliser ---------------------------------------------------

    /// <summary>
    /// A PNG assembled here rather than rendered, so the strip can be pinned
    /// without Blender. The CRC fields are zero: <see cref="PngNormalise"/>
    /// copies chunks verbatim and never validates one, which is deliberate —
    /// it is a filter, not a decoder, and a chunk it keeps must come out
    /// byte-for-byte as it went in.
    /// </summary>
    private static byte[] AbPng(params (string Type, byte[] Data)[] aChunks)
    {
        using MemoryStream o = new();
        o.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        foreach ((string strType, byte[] abData) in aChunks)
        {
            byte[] abLen = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(abLen, (uint)abData.Length);
            o.Write(abLen);
            o.Write(Encoding.ASCII.GetBytes(strType));
            o.Write(abData);
            o.Write(new byte[4]);
        }
        return o.ToArray();
    }

    private static byte[] AbTypicalBlenderPng() => AbPng(
        ("IHDR", new byte[13]),
        ("tEXt", Encoding.ASCII.GetBytes("Date\0 2026/09/11 22:39:42")),
        ("tEXt", Encoding.ASCII.GetBytes("RenderTime\0 00:00.62")),
        ("IDAT", [1, 2, 3, 4, 5]),
        ("IEND", []));

    [Fact]
    public void Strip_DropsTheClockChunksAndKeepsThePixels()
    {
        byte[] ab = PngNormalise.AbStrip(AbTypicalBlenderPng());
        Assert.Equal(["IHDR", "IDAT", "IEND"], PngNormalise.AstrChunkTypes(ab));
        Assert.DoesNotContain("2026/09/11", Encoding.ASCII.GetString(ab));
    }

    /// <summary>
    /// The point of the whole class: two renders that differ only in their
    /// timestamps must hash the same after stripping. This is the property the
    /// record's `image.sha256` is worth anything because of.
    /// </summary>
    [Fact]
    public void TwoRendersDifferingOnlyInTimestamps_StripToIdenticalBytes()
    {
        byte[] abA = PngNormalise.AbStrip(AbTypicalBlenderPng());
        byte[] abB = PngNormalise.AbStrip(AbPng(
            ("IHDR", new byte[13]),
            ("tEXt", Encoding.ASCII.GetBytes("Date\0 2026/09/11 22:39:45")),
            ("tEXt", Encoding.ASCII.GetBytes("RenderTime\0 00:00.50")),
            ("IDAT", [1, 2, 3, 4, 5]),
            ("IEND", [])));
        Assert.Equal(abA, abB);
        Assert.Equal(ArtifactStore.StrSha256(abA), ArtifactStore.StrSha256(abB));
    }

    /// <summary>
    /// A whitelist, not a blacklist of the two known keywords: a future Blender
    /// that writes a third timestamp under a name nobody predicted must not
    /// quietly break byte-identity.
    /// </summary>
    [Fact]
    public void AnUnknownAncillaryChunk_IsDroppedRatherThanTrusted()
    {
        byte[] ab = PngNormalise.AbStrip(AbPng(
            ("IHDR", new byte[13]),
            ("zzZz", Encoding.ASCII.GetBytes("some future clock")),
            ("IDAT", [9]),
            ("IEND", [])));
        Assert.Equal(["IHDR", "IDAT", "IEND"], PngNormalise.AstrChunkTypes(ab));
    }

    [Fact]
    public void ColourChunksSurvive_BecauseTheyChangeHowThePixelsAreRead()
    {
        byte[] ab = PngNormalise.AbStrip(AbPng(
            ("IHDR", new byte[13]),
            ("sRGB", [0]),
            ("gAMA", new byte[4]),
            ("tEXt", Encoding.ASCII.GetBytes("Date\0 x")),
            ("IDAT", [9]),
            ("IEND", [])));
        Assert.Equal(["IHDR", "sRGB", "gAMA", "IDAT", "IEND"], PngNormalise.AstrChunkTypes(ab));
    }

    [Fact]
    public void Strip_IsIdempotent()
    {
        byte[] ab1 = PngNormalise.AbStrip(AbTypicalBlenderPng());
        Assert.Equal(ab1, PngNormalise.AbStrip(ab1));
    }

    [Fact]
    public void NonPngBytes_AreRefusedRatherThanPassedThrough()
    {
        Assert.Throws<PngNormalise.PngException>(() => PngNormalise.AbStrip(Encoding.ASCII.GetBytes("not a png at all")));
        Assert.Throws<PngNormalise.PngException>(() => PngNormalise.AbStrip([]));
    }

    [Fact]
    public void APngWithNoIend_IsRefused()
    {
        byte[] ab = AbPng(("IHDR", new byte[13]), ("IDAT", [1]));
        Assert.Throws<PngNormalise.PngException>(() => PngNormalise.AbStrip(ab));
    }

    [Fact]
    public void AChunkClaimingMoreBytesThanExist_IsRefused()
    {
        byte[] ab = AbPng(("IHDR", new byte[13]), ("IDAT", [1]), ("IEND", []));
        BinaryPrimitives.WriteUInt32BigEndian(ab.AsSpan(8, 4), 0xFFFF_FFF0);
        Assert.Throws<PngNormalise.PngException>(() => PngNormalise.AbStrip(ab));
    }

    // ---- the real thing ---------------------------------------------------

    /// <summary>
    /// Blender when this machine has it: render run 50's artifact twice in two
    /// separate processes and require the same bytes. Returns early otherwise —
    /// CI has no Blender, and a check that cannot run must not look like one
    /// that passed.
    /// </summary>
    [Fact]
    public void RealBlender_RendersRun50TwiceToTheSameBytes()
    {
        string? strBlender = Environment.GetEnvironmentVariable("ODC_BLENDER");
        if (string.IsNullOrEmpty(strBlender) || !File.Exists(strBlender)) return;
        string strStl = Path.Combine(TestRegistry.StrRepoRoot(), "artifacts", "06",
            "06ac53848bc575284a835b83be4f033c7aef433ecd75be8ec974b3548aadf824.stl");
        if (!File.Exists(strStl)) return;

        (byte[] abA, ThumbnailSettings oA) = ThumbnailRender.ORender(strBlender, strStl, TimeSpan.FromMinutes(5));
        (byte[] abB, ThumbnailSettings oB) = ThumbnailRender.ORender(strBlender, strStl, TimeSpan.FromMinutes(5));

        Assert.Equal(ArtifactStore.StrSha256(abA), ArtifactStore.StrSha256(abB));

        // Measured: Blender 5.2.1 writes IHDR, sRGB, gAMA, cHRM, IDAT, IEND plus
        // the two tEXt chunks. The colour chunks stay — they say how to read the
        // pixels — and only the clock goes.
        IReadOnlyList<string> aTypes = PngNormalise.AstrChunkTypes(abA);
        Assert.Equal("IHDR", aTypes[0]);
        Assert.Equal("IEND", aTypes[^1]);
        Assert.Contains("IDAT", aTypes);
        Assert.DoesNotContain("tEXt", aTypes);

        // Same known answer as the cross-check's, arrived at down a different path.
        Assert.Equal(30.90, oA.ExtentX, 0.005);
        Assert.Equal(66.87, oA.ExtentY, 0.005);
        Assert.Equal(11.15, oA.ExtentZ, 0.005);
        Assert.Equal(oA.OrthoScale, oB.OrthoScale, 9);

        // The framing is arithmetic on the box, not a choice: the camera's
        // ortho scale is the box diagonal times the fixed margin. If this drifts,
        // "render it again and compare" has stopped being true.
        double fDiagonal = Math.Sqrt((oA.ExtentX * oA.ExtentX) + (oA.ExtentY * oA.ExtentY) + (oA.ExtentZ * oA.ExtentZ));
        Assert.Equal(fDiagonal * oA.Margin, oA.OrthoScale, 0.001);
    }

    [Fact]
    public void RenderRefusesWhenBlenderIsNotWhereItWasSaidToBe()
    {
        ThumbnailException e = Assert.Throws<ThumbnailException>(() =>
            ThumbnailRender.ORender(Path.Combine(Path.GetTempPath(), "no-such-blender.exe"),
                Path.Combine(Path.GetTempPath(), "no-such.stl"), TimeSpan.FromSeconds(5)));
        Assert.Contains("Blender not found", e.Message);
    }
}
