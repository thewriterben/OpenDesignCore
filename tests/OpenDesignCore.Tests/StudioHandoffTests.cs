using System.Net;
using System.Text;
using OpenDesignCore.Provenance;
using OpenDesignCore.Runs;
using Xunit;
using Xunit.Sdk;

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
    /// A loopback stub that mimics studio-core, and — the point of this class —
    /// one that can say which part of itself failed.
    ///
    /// Three separate fixture defects have now been diagnosed here, and the
    /// reason it took three is recorded in the revert of the last attempt:
    /// *"every failure mode of this fixture reads identically as 'AdvancedStudio
    /// unreachable', so three plausible stories all fit the evidence and none can
    /// be told apart from the output."* A failed bind, a pump thread that never
    /// ran, a request that never arrived, and a response that was never written
    /// all surfaced as one five-second timeout wearing the costume of a defect in
    /// the handoff code.
    ///
    /// So this records what it actually managed to do, and <see cref="StrDiagnose"/>
    /// turns that into the sentence the next failure should have printed.
    /// </summary>
    private sealed class StudioStub : IDisposable
    {
        private readonly HttpListener _oListener;
        private readonly Thread _oPump;
        private readonly ManualResetEventSlim _oPumping = new(false);
        private readonly List<string> _aRequests = [];
        private int _nAccepted;
        private int _nAnswered;
        private volatile Exception? _oPumpFault;

        public string StrUrl { get; }

        /// <summary>Snapshot, so a caller never enumerates a list the pump may still append to.</summary>
        public List<string> ARequests { get { lock (_aRequests) { return [.. _aRequests]; } } }

        public StudioStub(string strProposeResponse, int nProposeStatus)
        {
            (_oListener, StrUrl) = OBind();

            // A dedicated thread, not the thread pool.
            //
            // The code under test calls the studio synchronously
            // (`GetAsync(...).GetAwaiter().GetResult()`), so a test blocks a pool
            // thread for the whole round trip while the answer needs another pool
            // thread to produce it. On a two-core runner the pool starts near two
            // threads and injects roughly one per second, so the request can sit
            // unanswered until the five-second timeout fires. A fixture should not
            // compete for the pool the code under test is blocking, whatever else
            // turns out to be true.
            //
            // This was tried once and reverted, because on a plain Thread an
            // unhandled ObjectDisposedException from a stopped listener terminates
            // the *process* where the previous Task swallowed it — a run aborted at
            // 52 of 133 tests. The cause was that only GetContext() sat inside the
            // try: stopping the listener mid-request throws from the response write
            // instead, which nothing caught. Hence the whole body below is guarded,
            // and the fault recorded rather than discarded.
            _oPump = new Thread(() => Pump(strProposeResponse, nProposeStatus))
            {
                IsBackground = true,
                Name = "studio-stub",
            };
            _oPump.Start();

            // Bounded, and not load-bearing: HttpListener queues connections from
            // Start(), so a request arriving first is still served. It exists so
            // "the pump never ran" is a fact the diagnosis can state rather than a
            // possibility it has to hedge about.
            _oPumping.Wait(TimeSpan.FromSeconds(5));
        }

        private void Pump(string strProposeResponse, int nProposeStatus)
        {
            try
            {
                _oPumping.Set();
                while (_oListener.IsListening)
                {
                    HttpListenerContext oCtx = _oListener.GetContext();
                    Interlocked.Increment(ref _nAccepted);

                    string strPath = oCtx.Request.Url?.AbsolutePath ?? "";
                    string strBody;
                    using (StreamReader oReader = new(oCtx.Request.InputStream))
                    {
                        strBody = oReader.ReadToEnd();
                    }
                    lock (_aRequests)
                    {
                        _aRequests.Add($"{oCtx.Request.HttpMethod} {strPath} {strBody}");
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
                    Interlocked.Increment(ref _nAnswered);
                }
            }
            catch (Exception oEx)
            {
                // Stopping the listener is the ordinary way out of GetContext(), so
                // this is usually not a fault at all. Recording it either way costs
                // nothing and means a genuine one is never invisible.
                _oPumpFault = oEx;
            }
        }

        /// <summary>
        /// What the stub managed to do, in the order it would have done it. Read
        /// this when a test reports the studio unreachable: the studio is this
        /// object, and it is in-process.
        /// </summary>
        public string StrDiagnose()
        {
            bool bStopped = !_oListener.IsListening;
            string strFault = _oPumpFault is null
                ? "none recorded"
                : $"{_oPumpFault.GetType().Name}: {_oPumpFault.Message}"
                  + (bStopped
                      ? " (expected — the listener was stopped)"
                      : " (UNEXPECTED — the listener was still running)");

            List<string> aSeen = ARequests;
            return $"""
                Stub diagnosis (the "studio" here is an in-process stub, not a daemon):
                  bound at          {StrUrl}
                  pump thread ran   {(_oPumping.IsSet ? "yes" : "NO — it never reached GetContext()")}
                  requests accepted {Volatile.Read(ref _nAccepted)}
                  responses written {Volatile.Read(ref _nAnswered)}
                  pump fault        {strFault}
                  thread pool       {ThreadPool.ThreadCount} live, {ThreadPool.PendingWorkItemCount} queued, floor {StrMinThreads()}
                  requests seen     {(aSeen.Count == 0 ? "(none)" : string.Join(" | ", aSeen))}

                {StrReading()}
                """;
        }

        /// <summary>
        /// Pool depth, because "no request ever arrived" points at the client, and
        /// the client blocks a pool thread while needing pool threads to send. If a
        /// failure ever shows a starved pool here, that is the reading — no fifth
        /// theory required. See TestHostThreadPool.
        /// </summary>
        private static string StrMinThreads()
        {
            ThreadPool.GetMinThreads(out int nWorker, out int nCompletionPort);
            return $"{nWorker}w/{nCompletionPort}io";
        }

        private string StrReading()
        {
            if (!_oPumping.IsSet)
            {
                return "Reading: the pump thread never started. That is a fixture defect, not "
                     + "the handoff code.";
            }
            if (Volatile.Read(ref _nAccepted) == 0)
            {
                return "Reading: the stub was listening and no request ever arrived. Look at the "
                     + "client — proxy, timeout, wrong URL — not at the pump.";
            }
            if (Volatile.Read(ref _nAnswered) < Volatile.Read(ref _nAccepted))
            {
                return "Reading: a request was accepted and not answered. The pump stalled or "
                     + "faulted mid-response; see the fault above.";
            }
            return "Reading: every request accepted was answered, so a timeout here means the "
                 + "answer arrived too late rather than not at all.";
        }

        /// <summary>
        /// A listening stub on a port that was actually free, or a failure that says
        /// so. Tries a bounded number of ports and gives up loudly rather than
        /// returning a listener that is not listening — an earlier version surfaced
        /// a failed bind as "AdvancedStudio unreachable" too.
        /// </summary>
        private static (HttpListener oListener, string strUrl) OBind()
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

        public void Dispose()
        {
            _oListener.Stop();
            _oPump.Join(TimeSpan.FromSeconds(2));
            ((IDisposable)_oListener).Dispose();
            _oPumping.Dispose();
        }
    }

    private static StudioStub OStartStub(
        string strProposeResponse = """{"confirmation_id":"abc123def456","action":"print_start","will_run":"Start printing 'part.gcode'"}""",
        int nProposeStatus = 200)
        => new(strProposeResponse, nProposeStatus);

    /// <summary>
    /// Runs the handoff and, if it reports the studio unreachable, replaces that
    /// message with one that says which part of the fixture failed.
    ///
    /// "Unreachable" is never a real result in these tests — the studio is an
    /// in-process stub — so it is always a fixture defect, and converting it to a
    /// plain test failure also keeps it from being swallowed by an Assert.Throws
    /// that was waiting for a different HandoffException entirely.
    /// </summary>
    private static T TAgainst<T>(StudioStub oStub, Func<T> fn)
    {
        try
        {
            return fn();
        }
        catch (HandoffException oEx)
            when (oEx.Message.Contains("unreachable", StringComparison.Ordinal))
        {
            throw new XunitException(oEx.Message + "\n\n" + oStub.StrDiagnose());
        }
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
        using StudioStub oStub = OStartStub();
        string strUrl = oStub.StrUrl;
        HandoffResult oResult = TAgainst(oStub, () => StudioHandoff.Execute(
            StrArtifactsDir, StrLedgerPath, nRunId, StrStageDir, strUrl,
            strGcodeFilename: null, bOffline: false,
            strUploadFilename: "part.gcode"));

        Assert.Equal("upload-proposed", oResult.Status);
        Assert.Equal("abc123def456", oResult.UploadProposalId);
        Assert.Equal("", oResult.ProposalId);

        string strProposal = oStub.ARequests.Single(r => r.Contains("/api/propose"));
        Assert.Contains("\"action\":\"gcode_upload\"", strProposal);
        Assert.Contains("\"design_artifact_sha256\"", strProposal);
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
        using StudioStub oStub = OStartStub();
        string strUrl = oStub.StrUrl;
        HandoffResult oResult = TAgainst(oStub, () => StudioHandoff.Execute(
            StrArtifactsDir, StrLedgerPath, nRunId, StrStageDir, strUrl,
            strGcodeFilename: "part.gcode", bOffline: false,
            strUploadFilename: "part.gcode"));

        List<string> aRequests = oStub.ARequests;
        Assert.Equal(2, aRequests.Count(r => r.Contains("/api/propose")));
        Assert.NotEmpty(oResult.UploadProposalId);
        Assert.NotEmpty(oResult.ProposalId);
        Assert.Contains(aRequests, r => r.Contains("\"action\":\"gcode_upload\""));
        Assert.Contains(aRequests, r => r.Contains("\"action\":\"print_start\""));
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
        using StudioStub oStub = OStartStub(
            strProposeResponse: """{"detail":"resolves outside the staging directory"}""",
            nProposeStatus: 400);
        string strUrl = oStub.StrUrl;
        HandoffException oEx = Assert.Throws<HandoffException>(() =>
            TAgainst(oStub, () => StudioHandoff.Execute(
                StrArtifactsDir, StrLedgerPath, nRunId, StrStageDir, strUrl,
                strGcodeFilename: null, bOffline: false,
                strUploadFilename: "../escape.gcode")));

        Assert.Contains("gcode_upload proposal", oEx.Message);
        Assert.Contains("staging directory", oEx.Message);
    }

    [Fact]
    public void StagesVerifiesAndRecords()
    {
        long nRunId = NSeedRun();
        using StudioStub oStub = OStartStub();
        string strUrl = oStub.StrUrl;
        HandoffResult oResult = TAgainst(oStub, () => StudioHandoff.Execute(
            StrArtifactsDir, StrLedgerPath, nRunId, StrStageDir, strUrl,
            strGcodeFilename: null, bOffline: false));

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

    [Fact]
    public void ProposesPrintAndRecordsConfirmationId()
    {
        long nRunId = NSeedRun();
        using StudioStub oStub = OStartStub();
        string strUrl = oStub.StrUrl;
        HandoffResult oResult = TAgainst(oStub, () => StudioHandoff.Execute(
            StrArtifactsDir, StrLedgerPath, nRunId, StrStageDir, strUrl,
            strGcodeFilename: "part.gcode", bOffline: false));

        Assert.Equal("proposed", oResult.Status);
        Assert.Equal("abc123def456", oResult.ProposalId);

        // No lock: ARequests hands back a snapshot rather than the live list.
        string strPropose = Assert.Single(
            oStub.ARequests, s => s.StartsWith("POST /api/propose", StringComparison.Ordinal));
        Assert.Contains("\"print_start\"", strPropose);
        Assert.Contains("\"part.gcode\"", strPropose);

        using Ledger oLedger = new(StrLedgerPath);
        Assert.Equal("abc123def456", Assert.Single(oLedger.AHandoffs()).ProposalId);
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
