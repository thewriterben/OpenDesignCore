namespace OpenDesignCore.Runs;

/// <summary>
/// The HTTP client used to talk to AdvancedStudio, defined once.
///
/// <para>
/// <b>No proxy.</b> The studio is a local-first service — loopback or the local
/// network — so routing a request to it through a system proxy is never
/// correct. Worse, the default handler does not merely use a configured proxy,
/// it goes looking for one: on Windows, proxy auto-discovery (WPAD) runs before
/// the first byte moves and can take seconds on a machine with no proxy at all.
/// Against a five-second budget that is the difference between "the studio
/// answered" and "the studio is unreachable", decided by a network the request
/// was never going to cross.
/// </para>
///
/// <para>
/// Found on the first CI run that executed the test suite: a handoff making
/// three round trips timed out on a GitHub Windows runner while passing on a
/// developer machine. The timeout was not too short — the latency was not real.
/// </para>
///
/// <para>
/// One definition rather than two, because both callers need the same setting
/// for the same reason and a divergence would show up as an intermittent
/// timeout on one path only.
/// </para>
/// </summary>
internal static class StudioClient
{
    /// <summary>
    /// Seconds a studio call may take before it counts as unreachable. Declared
    /// here so both paths agree on what "unreachable" means.
    /// </summary>
    public const int NTimeoutSeconds = 5;

    public static HttpClient OCreate()
        => new(new HttpClientHandler { UseProxy = false, Proxy = null })
        {
            Timeout = TimeSpan.FromSeconds(NTimeoutSeconds),
        };
}
