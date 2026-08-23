using System.Runtime.CompilerServices;

namespace OpenDesignCore.Tests;

/// <summary>
/// Raises the test host's minimum thread-pool size before any test runs.
///
/// <para>
/// <b>This is the fourth diagnosis of one CI failure, and the first with direct
/// evidence.</b> <c>UploadAndPrintAreSeparateProposals</c> failed on Windows
/// runners as "AdvancedStudio unreachable" after five seconds. Three earlier
/// readings — proxy latency, a port collision, a starved stub — were guesses
/// that fit the output, because every failure mode of that fixture produced the
/// same sentence. Instrumenting the stub (see <c>StudioHandoffTests.StudioStub</c>)
/// replaced the guessing with a reading:
/// </para>
///
/// <code>
///   bound at          http://127.0.0.1:36254
///   pump thread ran   yes
///   requests accepted 0
///   responses written 0
///   pump fault        none recorded
/// </code>
///
/// <para>
/// The stub was bound, listening, and idle. <b>The request never arrived at
/// all</b>, which exonerates the fixture and moves the fault to the client
/// side — where <c>StudioHandoff</c> calls
/// <c>GetAsync(...).GetAwaiter().GetResult()</c>. Sync-over-async blocks a pool
/// thread for the whole round trip while the continuations that actually issue
/// the request need pool threads of their own. The pool starts at
/// <c>ProcessorCount</c> minimum threads — two on a standard runner — and
/// injects further threads at roughly one per second, so under xUnit's parallel
/// collections the send itself can miss a five-second budget. The previous
/// attempt moved the *stub* off the pool; the evidence above says the stub was
/// never the starved party.
/// </para>
///
/// <para>
/// <b>Why raise the floor rather than lengthen the timeout.</b> The five seconds
/// is a product decision recorded in <c>StudioClient</c>: it defines what
/// "unreachable" means to a user. Raising it to make a test pass would move a
/// user-facing threshold to accommodate the test host's scheduler, and the
/// CHANGELOG already rejected that reasoning once — *"the timeout was not too
/// short; the latency was not real."* The same applies here: nothing is slow,
/// something is unscheduled.
/// </para>
///
/// <para>
/// <b>What this does not claim.</b> It does not fix sync-over-async in
/// <c>StudioHandoff</c>, which is the underlying shape and would want the call
/// path made async to remove properly. That is a change to production code and
/// a CLI surface for a defect only ever observed in a parallel test host, so it
/// is recorded as a known shape rather than done here. If this reading is also
/// wrong, the stub diagnosis now reports pool depth alongside everything else,
/// and a fifth theory will not be needed to tell.
/// </para>
/// </summary>
internal static class TestHostThreadPool
{
    /// <summary>Enough headroom that injection latency never decides a timeout.</summary>
    private const int NFloor = 16;

    [ModuleInitializer]
    internal static void Raise()
    {
        ThreadPool.GetMinThreads(out int nWorker, out int nCompletionPort);
        ThreadPool.SetMinThreads(
            Math.Max(nWorker, NFloor),
            Math.Max(nCompletionPort, NFloor));
    }
}
