using System.Security.Cryptography;

namespace OpenDesignCore.Provenance;

/// <summary>
/// Content-addressed artifact store (ADR-0006): files land at
/// artifacts/&lt;sha256[0..2]&gt;/&lt;sha256&gt;&lt;extension&gt;. SHA-256 hex, matching
/// Project BINGO's asset content addressing so hashes are directly comparable
/// (wiki: bingo-odc-provenance-contract).
/// </summary>
public static class ArtifactStore
{
    public static string StrSha256(byte[] abData)
        => Convert.ToHexString(SHA256.HashData(abData)).ToLowerInvariant();

    /// <summary>Stores the bytes; returns the sha256 hex. Idempotent by content.</summary>
    public static string StrStore(string strArtifactsDir, byte[] abData, string strExtension)
    {
        string strHash = StrSha256(abData);
        string strDir = Path.Combine(strArtifactsDir, strHash[..2]);
        Directory.CreateDirectory(strDir);
        string strPath = Path.Combine(strDir, strHash + strExtension);
        if (!File.Exists(strPath))
            File.WriteAllBytes(strPath, abData);
        return strHash;
    }

    public static string StrPathFor(string strArtifactsDir, string strHash, string strExtension)
        => Path.Combine(strArtifactsDir, strHash[..2], strHash + strExtension);
}
