using System.Buffers.Binary;

namespace OpenDesignCore.Rendering;

/// <summary>
/// Strips the PNG chunks that carry a clock, so the same render twice is the
/// same bytes twice (ADR-0022).
///
/// Measured 2026-09-11, run 50 rendered in two separate Blender processes: every
/// IDAT chunk identical, decompressed pixels identical, and exactly two tEXt
/// chunks different —
///
///     tEXt  Date = 2026/09/11 22:39:42   vs   2026/09/11 22:39:45
///     tEXt  RenderTime = 00:00.62        vs   00:00.50
///
/// — a wall clock and a stopwatch. So the renderer *is* reproducible and only
/// its metadata is not, which is the difference between "we cannot honour
/// determinism here" and "we strip two fields". The date the render happened is
/// not lost: it is in the ledger row, where a time belongs.
///
/// Deliberately a whitelist rather than a blacklist of those two keywords. A
/// future Blender that adds another timestamp keyword would silently break
/// byte-identity under a blacklist; under this, an unknown ancillary chunk is
/// dropped and the image still renders, because ancillary chunks are by
/// definition optional to a decoder.
/// </summary>
public static class PngNormalise
{
    private static readonly byte[] AbSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Chunks kept, in the order the file presents them. These are the ones that
    /// change how the pixels are *interpreted*; everything else is provenance
    /// the record already carries, or a timestamp.
    /// </summary>
    private static readonly HashSet<string> AstrKeep =
        ["IHDR", "PLTE", "tRNS", "gAMA", "cHRM", "sRGB", "iCCP", "sBIT", "IDAT", "IEND"];

    /// <summary>Thrown when the bytes are not a PNG this can reason about.</summary>
    public sealed class PngException(string strMessage) : Exception(strMessage);

    /// <summary>
    /// Return <paramref name="abPng"/> with non-essential chunks removed. Pure:
    /// no clock, no filesystem, so a test can pin it without Blender.
    /// </summary>
    public static byte[] AbStrip(byte[] abPng)
    {
        if (abPng.Length < 8 || !abPng.AsSpan(0, 8).SequenceEqual(AbSignature))
            throw new PngException("not a PNG: signature missing");

        using MemoryStream oOut = new(abPng.Length);
        oOut.Write(AbSignature);

        int i = 8;
        bool bSawEnd = false;
        while (i + 8 <= abPng.Length)
        {
            uint uLength = BinaryPrimitives.ReadUInt32BigEndian(abPng.AsSpan(i, 4));
            if (uLength > int.MaxValue - 12)
                throw new PngException($"chunk at byte {i} declares an impossible length");
            int nLength = (int)uLength;
            if (i + 12 + nLength > abPng.Length)
                throw new PngException($"chunk at byte {i} runs past the end of the file");

            string strType = System.Text.Encoding.ASCII.GetString(abPng, i + 4, 4);
            if (AstrKeep.Contains(strType))
                oOut.Write(abPng.AsSpan(i, 12 + nLength));
            if (strType == "IEND")
            {
                bSawEnd = true;
                break;
            }
            i += 12 + nLength;
        }

        if (!bSawEnd)
            throw new PngException("PNG has no IEND chunk");
        return oOut.ToArray();
    }

    /// <summary>The chunk types present, in file order. For tests and diagnostics.</summary>
    public static IReadOnlyList<string> AstrChunkTypes(byte[] abPng)
    {
        if (abPng.Length < 8 || !abPng.AsSpan(0, 8).SequenceEqual(AbSignature))
            throw new PngException("not a PNG: signature missing");
        List<string> aOut = [];
        int i = 8;
        while (i + 8 <= abPng.Length)
        {
            uint uLength = BinaryPrimitives.ReadUInt32BigEndian(abPng.AsSpan(i, 4));
            if (uLength > int.MaxValue - 12 || i + 12 + uLength > abPng.Length)
                throw new PngException($"chunk at byte {i} is malformed");
            string strType = System.Text.Encoding.ASCII.GetString(abPng, i + 4, 4);
            aOut.Add(strType);
            if (strType == "IEND")
                break;
            i += 12 + (int)uLength;
        }
        return aOut;
    }
}
