using Microsoft.Data.Sqlite;

namespace OpenDesignCore.Provenance;

/// <summary>One run row. Timestamps live here, never in the provenance sidecar —
/// the sidecar must be byte-identical across reruns (ADR-0003).</summary>
public sealed record RunRecord
{
    public long Id { get; init; }
    public required string CreatedUtc { get; init; }
    public required string Model { get; init; }
    public required string VoxelSizeMm { get; init; }
    public required string InputsJson { get; init; }
    public required string VersionsJson { get; init; }
    public required string ArtifactSha256 { get; init; }
    public required string ProvenanceSha256 { get; init; }
    public required bool Passed { get; init; }
}

/// <summary>One fabrication-handoff row: a run's artifact leaving ODC for a fabricator.</summary>
public sealed record HandoffRecord
{
    public long Id { get; init; }
    public required string CreatedUtc { get; init; }
    public required long RunId { get; init; }
    public required string ArtifactSha256 { get; init; }
    public required string Destination { get; init; }
    /// <summary>staged | staged-offline | proposed</summary>
    public required string Status { get; init; }
    public required string StagedPath { get; init; }
    /// <summary>Studio confirmation id when a print was proposed; empty otherwise.</summary>
    public required string ProposalId { get; init; }
}

/// <summary>
/// Append-only run ledger (ADR-0006): SQLite, written by code only. The public
/// surface is insert and read — there is deliberately no update or delete.
/// </summary>
public sealed class Ledger : IDisposable
{
    private readonly SqliteConnection _oConn;

    public Ledger(string strDbPath)
    {
        // Pooling off: the pool holds the file handle after Dispose, which
        // breaks cleanup and belies the "single writer, open briefly" model.
        _oConn = new SqliteConnection($"Data Source={strDbPath};Pooling=False");
        _oConn.Open();
        using SqliteCommand oCmd = _oConn.CreateCommand();
        oCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS runs (
                id                INTEGER PRIMARY KEY AUTOINCREMENT,
                created_utc       TEXT NOT NULL,
                model             TEXT NOT NULL,
                voxel_size_mm     TEXT NOT NULL,
                inputs_json       TEXT NOT NULL,
                versions_json     TEXT NOT NULL,
                artifact_sha256   TEXT NOT NULL,
                provenance_sha256 TEXT NOT NULL,
                passed            INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS handoffs (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                created_utc     TEXT NOT NULL,
                run_id          INTEGER NOT NULL,
                artifact_sha256 TEXT NOT NULL,
                destination     TEXT NOT NULL,
                status          TEXT NOT NULL,
                staged_path     TEXT NOT NULL,
                proposal_id     TEXT NOT NULL
            );
            """;
        oCmd.ExecuteNonQuery();
    }

    public RunRecord? ORunById(long nId) => ARuns().FirstOrDefault(o => o.Id == nId);

    public long NAppendHandoff(HandoffRecord oHandoff)
    {
        using SqliteCommand oCmd = _oConn.CreateCommand();
        oCmd.CommandText = """
            INSERT INTO handoffs (created_utc, run_id, artifact_sha256, destination,
                                  status, staged_path, proposal_id)
            VALUES ($created, $run, $artifact, $dest, $status, $staged, $proposal);
            SELECT last_insert_rowid();
            """;
        oCmd.Parameters.AddWithValue("$created", oHandoff.CreatedUtc);
        oCmd.Parameters.AddWithValue("$run", oHandoff.RunId);
        oCmd.Parameters.AddWithValue("$artifact", oHandoff.ArtifactSha256);
        oCmd.Parameters.AddWithValue("$dest", oHandoff.Destination);
        oCmd.Parameters.AddWithValue("$status", oHandoff.Status);
        oCmd.Parameters.AddWithValue("$staged", oHandoff.StagedPath);
        oCmd.Parameters.AddWithValue("$proposal", oHandoff.ProposalId);
        return (long)oCmd.ExecuteScalar()!;
    }

    public IReadOnlyList<HandoffRecord> AHandoffs()
    {
        List<HandoffRecord> aHandoffs = [];
        using SqliteCommand oCmd = _oConn.CreateCommand();
        oCmd.CommandText = """
            SELECT id, created_utc, run_id, artifact_sha256, destination,
                   status, staged_path, proposal_id
            FROM handoffs ORDER BY id;
            """;
        using SqliteDataReader oReader = oCmd.ExecuteReader();
        while (oReader.Read())
        {
            aHandoffs.Add(new HandoffRecord
            {
                Id = oReader.GetInt64(0),
                CreatedUtc = oReader.GetString(1),
                RunId = oReader.GetInt64(2),
                ArtifactSha256 = oReader.GetString(3),
                Destination = oReader.GetString(4),
                Status = oReader.GetString(5),
                StagedPath = oReader.GetString(6),
                ProposalId = oReader.GetString(7),
            });
        }
        return aHandoffs;
    }

    public long NAppend(RunRecord oRun)
    {
        using SqliteCommand oCmd = _oConn.CreateCommand();
        oCmd.CommandText = """
            INSERT INTO runs (created_utc, model, voxel_size_mm, inputs_json,
                              versions_json, artifact_sha256, provenance_sha256, passed)
            VALUES ($created, $model, $voxel, $inputs, $versions, $artifact, $provenance, $passed);
            SELECT last_insert_rowid();
            """;
        oCmd.Parameters.AddWithValue("$created", oRun.CreatedUtc);
        oCmd.Parameters.AddWithValue("$model", oRun.Model);
        oCmd.Parameters.AddWithValue("$voxel", oRun.VoxelSizeMm);
        oCmd.Parameters.AddWithValue("$inputs", oRun.InputsJson);
        oCmd.Parameters.AddWithValue("$versions", oRun.VersionsJson);
        oCmd.Parameters.AddWithValue("$artifact", oRun.ArtifactSha256);
        oCmd.Parameters.AddWithValue("$provenance", oRun.ProvenanceSha256);
        oCmd.Parameters.AddWithValue("$passed", oRun.Passed ? 1 : 0);
        return (long)oCmd.ExecuteScalar()!;
    }

    public IReadOnlyList<RunRecord> ARuns()
    {
        List<RunRecord> aRuns = [];
        using SqliteCommand oCmd = _oConn.CreateCommand();
        oCmd.CommandText = """
            SELECT id, created_utc, model, voxel_size_mm, inputs_json,
                   versions_json, artifact_sha256, provenance_sha256, passed
            FROM runs ORDER BY id;
            """;
        using SqliteDataReader oReader = oCmd.ExecuteReader();
        while (oReader.Read())
        {
            aRuns.Add(new RunRecord
            {
                Id = oReader.GetInt64(0),
                CreatedUtc = oReader.GetString(1),
                Model = oReader.GetString(2),
                VoxelSizeMm = oReader.GetString(3),
                InputsJson = oReader.GetString(4),
                VersionsJson = oReader.GetString(5),
                ArtifactSha256 = oReader.GetString(6),
                ProvenanceSha256 = oReader.GetString(7),
                Passed = oReader.GetInt64(8) != 0,
            });
        }
        return aRuns;
    }

    public void Dispose() => _oConn.Dispose();
}
