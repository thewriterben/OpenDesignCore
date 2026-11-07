using System.Reflection;
using PicoGK;

namespace OpenDesignCore;

/// <summary>
/// The PicoGK <see cref="Library"/> every run and test in this repository
/// opens. It differs only in that it stops the library's memory-monitor timer
/// as soon as it is constructed, before the timer's first tick.
/// </summary>
/// <remarks>
/// <para>
/// PicoGK 2.2.0 starts a 10-second <see cref="Timer"/> in the Library
/// constructor. Each tick calls the native memory counter on a thread-pool
/// thread, to report native memory to the GC. The test host died with
/// 0xC0000005 in <c>Library._nTotalMemUsage</c> under
/// <c>Library.MonitorMemory</c>, at 173 and 180 tests in two of three parallel
/// runs (2026-11-07).
/// </para>
/// <para>
/// Two causes, and only the second needs this class to stop the timer rather
/// than wait for it. First, 2.2.0's Dispose destroys the native instance
/// without waiting for a callback already queued; upstream fixed that in
/// leap71/PicoGK 53444e4d (2026-08-22), which no NuGet release has yet. Second,
/// the crash persisted (two of three runs again) with Dispose made to wait for
/// queued callbacks, so the counter also faults on a live library: the native
/// call is not safe while the owning thread is working with that library, and
/// upstream's fix (a lock taken only by Dispose and the monitor) does not
/// cover that.
/// </para>
/// <para>
/// What is lost: the GC is no longer told about native memory, so it collects
/// undisposed PicoGK objects less eagerly. Every library here is opened with
/// <c>using</c> and destroying it frees its native objects, so the cost is
/// bounded by one run. Revisit when PicoGK makes the monitor optional or safe.
/// </para>
/// </remarks>
public sealed class OdcLibrary : Library
{
    /// <summary>
    /// The timer field, by name. If a PicoGK upgrade renames it, this throws
    /// on first use rather than letting the crash back in silently.
    /// </summary>
    public static readonly FieldInfo OTimerField =
        typeof(Library).GetField("m_oTimerMemCheck", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(
            "PicoGK.Library.m_oTimerMemCheck is gone. Check whether this PicoGK version still " +
            "needs OdcLibrary (see its remarks) before changing the field name.");

    public OdcLibrary(float fVoxelSizeMm) : base(fVoxelSizeMm)
    {
        // Dispose(WaitHandle) also waits out a callback already running, which
        // cannot happen this early (the first tick is 10 s away) but costs
        // nothing to rule out.
        Timer oTimer = (Timer)OTimerField.GetValue(this)!;
        using ManualResetEvent oCallbacksDone = new(false);
        if (oTimer.Dispose(oCallbacksDone))
            oCallbacksDone.WaitOne();
    }
}
