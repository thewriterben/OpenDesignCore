using System.Net;
using System.Text;
using OpenDesignCore.Provenance;
using OpenDesignCore.Runs;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// Handoff tests against a loopback stub that mimics studio-core's surface
/// (GET /api/state, POST /api/propose) as surveyed 2026-08-15. The live studio
/// is exercised manually; these pin ODC's side of the contract.
/// </summary>
public sealed class StudioHandoffTests : IDisposable
{
    private readonly string _strTempDir =
        Directory.CreateTempSubdirectory("odc-handoff-tests").FullName;

    public void Dispose() => Directory.Delete(_strTempDir, recursive: true);

    private string StrArtifactsDir => Path.Combine(_strTempDir, "artifacts");
    private string StrLedgerPath => Path.Combine(_strTempDir, "ledger.db");
    private string StrStageDir => Path.Combine(_strTempDir, "stage");

    /// <summary>Seed a run row + matching artifact/sidecar without geometry.</summary>
    private long NSeedRun()
    {
        byte[] abStl = Encoding.ASCII.GetBytes("fake-stl-bytes");
        byte[] abSidecar = Encoding.ASCII.GetBytes("""{"schema":"odc/provenance/0.2"}""");
        string strArtifact = ArtifactStore.StrStore(StrArtifactsDir, abStl, ".stl");
        string strSidecar = ArtifactStore.StrStore(StrArtifactsDir, abSidecar, ".provenance.json");

        using Ledger oLedger = new(StrLedgerPath);
        return oLedger.NAppend(new RunRecord
        {
            CreatedUtc = "2026-08-15T00:00:00.0000000Z",
            Model = "enclosure-shell/0.1",
            VoxelSizeMm = "0.20",
            InputsJson = "{}",
            VersionsJson = "{}",
            ArtifactSha256 = strArtifact,
            ProvenanceSha256 = strSidecar,
            Passed = true,
        });
    }

    /// <summary>
    /// A listening stub on a port that was actually free, or a failure that
    /// says so. Tries a bounded number of ports and gives up loudly rather
    /// than returning a listener that is not listening — the previous version
    /// surfaced a failed bind as "AdvancedStudio unreachable", which reads as
    /// a defect in the code under test rather than in the fixture.
    /// </summary>
    private static (HttpListener oListener, string strUrl) OBindStub()
    {
        const int nAttempts = 20;
        for (int i = 0; i < nAttempts; i++)
        {
            HttpListener oListener = new();
            string strUrl = $"http://127.0.0.1:{Random.Shared.Next(20000, 49000)}";
            oListener.Prefixes.Add(strUrl + "/");
            try
            {
                oListener.Start();
                return (oListener, strUrl);
            }
            catch (HttpListenerException)
            {
                ((IDisposable)oListener).Dispose();
            }
        }
        throw new InvalidOperationException(
            $"Could not bind a loopback stub in {nAttempts} attempts. This is the test "
            + "fixture failing, not the handoff code.");
    }

    private static (HttpListener, string strUrl, List<string> aRequests) OStartStub(
        string strProposeResponse = """{"confirmation_id":"abc123def456","action":"print_start","will_run":"Start printing 'part.gcode'"}""",
        int nProposeStatus = 200)
    {
        // Bind with retry rather than hoping. The port was picked at random
        // from 20000-60000 and used without checking: Windows hands out
        // ephemeral ports from 49152 upward, so on a busy machine that span is
        // sometimes already taken and Start() throws ERROR_SHARING_VIOLATION.
        // It never lost that lottery on a developer machine and lost it three
        // times in one CI run, which is why it looked like one flaky test
        // rather than a helper that cannot bind.
        //
        // Randomness here is not seeded, and deliberately so: the port does not
        // reach any output, so nothing about the result depends on which one is
        // used. Availability is the only thing being decided.
        (HttpListener oListener, string strUrl) = OBindStub();
        List<string> aRequests = [];

        _ = Task.Run(async () =>
        {
            while (oListener.IsListening)
            {
                HttpListenerContext oCtx;
                try
                {
                    oCtx = await oListener.GetContextAsync();
                }
                catch (Exception)
                {
                    return; // listener stopped
                }
                string strPath = oCtx.Request.Url?.AbsolutePath ?? "";
                string strBody = new StreamReader(oCtx.Request.InputStream).ReadToEnd();
                lock (aRequests)
                {
                    aRequests.Add($"{oCtx.Request.HttpMethod} {strPath} {strBody}");
                }
                byte[] abResp = Encoding.UTF8.GetBytes(strPath switch
                {
                    "/api/state" => """{"status":{"state":"ready"}}""",
                    "/api/propose" => strProposeResponse,
                    _ => """{"error":"not found"}""",
                });
                oCtx.Response.StatusCode = strPath switch
                {
                    "/api/state" => 200,
                    "/api/propose" => nProposeStatus,
                    _ => 404,
                };
                oCtx.Response.OutputStream.Write(abResp);
                oCtx.Response.Close();
            }
        });
        return (oListener, strUrl, aRequests);
    }

    /// <summary>
    /// The upload seam. A sliced job reaches the printer without anyone
    /// copying a file by hand, and the design hash travels with it so the
    /// human approving sees which design the job claims to be.
    /// </summary>
    [Fact]
    public void ProposesUploadCarryingTheDesignHash()
    {
        long nRunId = NSeedRun();
        (HttpListener oStub, string strUrl, List<string> aRequests) = OStartStub();
        try
        {
            HandoffResult oResult = StudioHandoff.Execute(
                StrArtifactsDir, StrLedgerPath, nRunId, StrStageDir, strUrl,
                strGcodeFilename: null, bOffline: false,
                strUploadFilename: "part.gcode");

            Assert.Equal("upload-proposed", oResult.Status);
            Assert.Equal("abc123def456", oResult.UploadProposalId);
            Assert.Equal("", oResult.ProposalId);

            string strProposal = aRequests.Single(r => r.Contains("/api/propose"));
            Assert.Contains("\"action\":\"gcode_upload\"", strProposal);
            Assert.Contains("\"design_artifact_sha256\"", strProposal);
        }
        finally
        {
            oStub.Stop();
        }
    }

    /// <summary>
    /// Two proposals, never one. Putting a file on a printer and starting a
    /// print are separate effects; bundling them would hide the second behind
    /// approval of the first.
    /// </summary>
    [Fact]
    public void UploadAndPrintAreSeparateProposals()
    {
        long nRunId = NSeedRun();
        (HttpListener oStub, string strUrl, List<string> aRequests) = OStartStub();
        try
        {
            HandoffResult oResult = StudioHandoff.Execute(
                StrArtifactsDir, StrLedgerPath, nRunId, StrStageDir, strUrl,
                strGcodeFilename: "part.gcode", bOffline: false,
                strUploadFilename: "part.gcode");

            Assert.Equal(2, aRequests.Count(r => r.Contains("/api/propose")));
            Assert.NotEmpty(oResult.UploadProposalId);
            Assert.NotEmpty(oResult.ProposalId);
            Assert.Contains(aRequests, r => r.Contains("\"action\":\"gcode_upload\""));
            Assert.Contains(aRequests, r => r.Contains("\"action\":\"print_start\""));
        }
        finally
        {
            oStub.Stop();
        }
    }

    [Fact]
    public void OfflineRefusesAnUploadRatherThanIgnoringIt()
    {
        long nRunId = NSeedRun();
        HandoffException oEx = Assert.Throws<HandoffException>(() =>
            StudioHandoff.Execute(
                StrArtifactsDir, StrLedgerPath, nRunId, StrStageDir, "http://unused",
                strGcodeFilename: null, bOffline: true, strUploadFilename: "part.gcode"));
        Assert.Contains("require the studio", oEx.Message);
    }

    [Fact]
    public void AStudioRefusalNamesTheActionItRefused()
    {
        long nRunId = NSeedRun();
        (HttpListener oStub, string strUrl, _) = OStartStub(
            strProposeResponse: """{"detail":"resolves outside the staging directory"}""",
            nProposeStatus: 400);
        try
        {
            HandoffException oEx = Assert.Throws<HandoffException>(() =>
                StudioHandoff.Execute(
                    StrArtifactsDir, StrLedgerPath, nRunId, StrStageDir, strUrl,
                    strGcodeFilename: null, bOffline: false,
                    strUploadFilename: "../escape.gcode"));

            Assert.Contains("gcode_upload proposal", oEx.Message);
            Assert.Contains("staging directory", oEx.Message);
        }
        finally
        {
            oStub.Stop();
        }
    }

    [Fact]
    public void StagesVerifiesAndRecords()
    {
        long nRunId = NSeedRun();
        (HttpListener oStub, string strUrl, _) = OStartStub();
        try
        {
            HandoffResult oResult = StudioHandoff.Execute(
                StrArtifactsDir, StrLedgerPath, nRunId, StrStageDir, strUrl,
                strGcodeFilename: null, bOffline: false);

            Assert.Equal("staged", oResult.Status);
            Assert.True(File.Exists(oResult.StagedStlPath));
            Assert.True(File.Exists(oResult.StagedStlPath[..^4] + ".provenance.json"));
            Assert.Contains("enclosure-shell-0.1-", Path.GetFileName(oResult.StagedStlPath));

            using Ledger oLedger = new(StrLedgerPath);
            HandoffRecord oRow = Assert.Single(oLedger.AHandoffs());
            Assert.Equal(nRunId, oRow.RunId);
            Assert.Equal("staged", oRow.Status);
            Assert.Equal("", oRow.ProposalId);
        }
        finally
        {
            oStub.Stop();
        }
    }

    [Fact]
    public void ProposesPrintAndRecordsConfirmationId()
    {
        long nRunId = NSeedRun();
        (HttpListener oStub, string strUrl, List<string> aRequests) = OStartStub();
        try
        {
            HandoffResult oResult = StudioHandoff.Execute(
                StrArtifactsDir, StrLedgerPath, nRunId, StrStageDir, strUrl,
                strGcodeFilename: "part.gcode", bOffline: false);

            Assert.Equal("proposed", oResult.Status);
            Assert.Equal("abc123def456", oResult.ProposalId);

            lock (aRequests)
            {
                string strPropose = Assert.Single(aRequests, s => s.StartsWith("POST /api/propose", StringComparison.Ordinal));
                Assert.Contains("\"print_start\"", strPropose);
                Assert.Contains("\"part.gcode\"", strPropose);
            }

            using Ledger oLedger = new(StrLedgerPath);
            Assert.Equal("abc123def456", Assert.Single(oLedger.AHandoffs()).ProposalId);
        }
        finally
        {
            oStub.Stop();
        }
    }

    [Fact]
    public void UnreachableStudio_FailsLoudly()
    {
        long nRunId = NSeedRun();
        HandoffException oEx = Assert.Throws<HandoffException>(() => StudioHandoff.Execute(
            StrArtifactsDir, StrLedgerPath, nRunId, StrStageDir,
            "http://127.0.0.1:1", strGcodeFilename: null, bOffline: false));
        Assert.Contains("unreachable", oEx.Message);

        using Ledger oLedger = new(StrLedgerPath);
        Assert.Empty(oLedger.AHandoffs()); // nothing recorded as if it succeeded
    }

    [Fact]
    public void Offline_RecordsStagedOffline()
    {
        long nRunId = NSeedRun();
        HandoffResult oResult = StudioHandoff.Execute(
            StrArtifactsDir, StrLedgerPath, nRunId, StrStageDir,
            "http://127.0.0.1:1", strGcodeFilename: null, bOffline: true);

        Assert.Equal("staged-offline", oResult.Status);
        using Ledger oLedger = new(StrLedgerPath);
        Assert.StartsWith("offline:", Assert.Single(oLedger.AHandoffs()).Destination);
    }

    [Fact]
    public void OfflineWithPrint_IsContradictoryAndRefused()
    {
        long nRunId = NSeedRun();
        Assert.Throws<HandoffException>(() => StudioHandoff.Execute(
            StrArtifactsDir, StrLedgerPath, nRunId, StrStageDir,
            "http://127.0.0.1:1", strGcodeFilename: "part.gcode", bOffline: true));
    }
}
