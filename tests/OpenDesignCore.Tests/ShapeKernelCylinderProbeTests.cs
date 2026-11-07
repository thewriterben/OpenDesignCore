using System.Numerics;
using PicoGK;
using Xunit;
using Xunit.Abstractions;

namespace OpenDesignCore.Tests;

/// <summary>
/// Why the pinned ShapeKernel submodule is unusable from this codebase, and how
/// a round hole gets made instead.
///
/// <para>
/// ADR-0008 pins LEAP71 ShapeKernel as a submodule, compiles it, and references
/// it. Nothing has ever called it. The reason turns out not to be neglect.
/// </para>
///
/// <para>
/// ShapeKernel's shapes build through the parameterless <c>new Mesh()</c>, which
/// resolves PicoGK's <b>ambient library singleton</b> — populated only by
/// <c>Library.Go(...)</c>. This repository does not use <c>Library.Go</c>: every
/// run does <c>using (Library oLib = new(voxelSize))</c> and threads
/// <c>oLib</c> explicitly into the Library-taking overloads. Under that pattern
/// ShapeKernel throws <c>"Your code relies on being called using Library::Go"</c>
/// before it draws anything. Measured 2026-09-25.
/// </para>
///
/// <para>
/// <b>ADR-0003 and ARCHITECTURE.md both say the voxel size is set "through
/// <c>Library.Go</c>", and the code says otherwise.</b> That divergence was
/// recorded as harmless doc drift. It is not harmless — it is exactly what makes
/// a pinned, compiled, referenced dependency unreachable, and it went unnoticed
/// because nothing depended on the dependency. Reconciling the two is an ADR,
/// not a drive-by fix, because every run's structure changes.
/// </para>
///
/// <para>
/// <b>This file deliberately does not construct a ShapeKernel shape to prove
/// that.</b> The first version did, and it crashed the test host: the throw
/// happens inside <c>Mesh</c>'s own constructor, leaving a partially built
/// object on the finalizer queue, and <c>Mesh.Finalize</c> then dereferences a
/// field that was never initialised — a <c>NullReferenceException</c> on the
/// finalizer thread, which no <c>catch</c> can reach. The suite reported 232,
/// then 85, then 83 tests on consecutive runs before the cause was found.
/// Upstream's bug (an unguarded <c>Dispose</c> after a throwing constructor),
/// worked around by not calling it, per ADR-0001.
/// </para>
/// </summary>
public sealed class ShapeKernelCylinderProbeTests(ITestOutputHelper oOutput)
{
    private const float FVoxelMm = 0.10f;
    private const float FRadiusMm = 7.475f;   // the measured barrel, 14.95 mm across
    private const float FLengthMm = 12.0f;

    /// <summary>
    /// An expiry assertion, in the shape ADR-0021 established: it asserts a
    /// known limitation is <i>still present</i> and fails when it stops being
    /// true.
    ///
    /// <para>
    /// It checks the <b>condition</b> — that runs construct a Library directly
    /// rather than through <c>Library.Go</c> — rather than the symptom, because
    /// triggering the symptom takes the test host down (see the class remarks).
    /// If this goes red, someone has moved to <c>Library.Go</c>, ShapeKernel has
    /// become reachable, and the lattice-beam workaround below can be
    /// reconsidered instead of silently kept forever.
    /// </para>
    /// </summary>
    [Fact]
    public void RunsStillConstructTheLibraryDirectlySoShapeKernelStaysUnreachable()
    {
        string strRunsDir = Path.Combine(StrRepoRoot(), "src", "OpenDesignCore", "Runs");
        string[] aRuns = Directory.GetFiles(strRunsDir, "*.cs");
        Assert.NotEmpty(aRuns);

        List<string> aDirect = [];
        List<string> aViaGo = [];
        foreach (string strFile in aRuns)
        {
            string strText = File.ReadAllText(strFile);
            if (strText.Contains("new(", StringComparison.Ordinal)
                && strText.Contains("Library oLib", StringComparison.Ordinal))
            {
                aDirect.Add(Path.GetFileName(strFile));
            }
            if (strText.Contains("Library.Go", StringComparison.Ordinal))
                aViaGo.Add(Path.GetFileName(strFile));
        }

        oOutput.WriteLine(
            $"runs constructing Library directly: {string.Join(", ", aDirect)}\n"
            + $"runs using Library.Go: {(aViaGo.Count == 0 ? "(none)" : string.Join(", ", aViaGo))}");

        Assert.NotEmpty(aDirect);
        Assert.True(
            aViaGo.Count == 0,
            $"[{string.Join(", ", aViaGo)}] now use Library.Go. ShapeKernel needs the ambient "
            + "singleton that Library.Go populates, so it may now be reachable — revisit "
            + "whether PicoGK's lattice beam is still the right way to make a cylinder here, "
            + "and reconcile ADR-0003 with the code while you are there.");
    }

    private static string StrRepoRoot()
    {
        DirectoryInfo? oDir = new(AppContext.BaseDirectory);
        while (oDir is not null && !File.Exists(Path.Combine(oDir.FullName, "OpenDesignCore.sln")))
            oDir = oDir.Parent;
        Assert.NotNull(oDir);
        return oDir.FullName;
    }

    /// <summary>
    /// The cylinder this repo can actually make: PicoGK's own lattice beam,
    /// which has a Library-taking overload and so works under the pattern every
    /// run already uses.
    ///
    /// <para>
    /// Its ends are hemispherical, per the package's own documentation. For a
    /// through-bore that is not a problem and is arguably a feature — run the
    /// beam past both faces and the caps land outside the material, leaving a
    /// true cylinder through it. The arithmetic below accounts for them, so the
    /// check is on the real solid rather than an idealised one.
    /// </para>
    /// </summary>
    [Fact]
    public void ALatticeBeamHasTheVolumeItsRadiusAndLengthImply()
    {
        Vector3 vecA = new(0, 0, 0);
        Vector3 vecB = new(0, 0, FLengthMm);

        // Cylinder between the endpoints, plus two hemispheres = one sphere.
        double fExact = (Math.PI * FRadiusMm * FRadiusMm * FLengthMm)
                      + (4.0 / 3.0 * Math.PI * FRadiusMm * FRadiusMm * FRadiusMm);

        double fArea = (2 * Math.PI * FRadiusMm * FLengthMm)
                     + (4 * Math.PI * FRadiusMm * FRadiusMm);
        double fBound = fArea * FVoxelMm;

        double fMeasured;
        BBox3 oBox;
        using (Library oLib = new(FVoxelMm))
        {
            Voxels voxBeam = Voxels.voxLatticeBeam(oLib, vecA, FRadiusMm, vecB, FRadiusMm);
            voxBeam.CalculateProperties(out float fVolume, out BBox3 oBBox);
            fMeasured = fVolume;
            oBox = oBBox;
        }

        Vector3 vecSize = oBox.vecSize();
        double fError = Math.Abs(fMeasured - fExact);

        oOutput.WriteLine(
            $"radius {FRadiusMm} mm, axis length {FLengthMm} mm, voxel {FVoxelMm} mm\n"
            + $"  analytic {fExact:F3} mm³ (cylinder + two hemispheres), "
            + $"voxel {fMeasured:F3} mm³, error {fError:F3} mm³ "
            + $"({fError / fExact * 100:F3} %), bound {fBound:F3} mm³\n"
            + $"  bbox size {vecSize.X:F3} x {vecSize.Y:F3} x {vecSize.Z:F3} mm");

        Assert.True(
            fError <= fBound,
            $"voxLatticeBeam's volume {fMeasured:F3} mm³ differs from the analytic "
            + $"{fExact:F3} mm³ by {fError:F3} mm³, beyond the {fBound:F3} mm³ a one-voxel "
            + "band permits.");
    }

    /// <summary>
    /// The diameter is the whole point of a bore, so it is asserted rather than
    /// assumed: exactly two radii across, with the hemispheres extending exactly
    /// one radius past each axis endpoint.
    /// </summary>
    [Fact]
    public void ALatticeBeamIsTwoRadiiAcrossAndCappedByOneRadius()
    {
        BBox3 oBox;
        using (Library oLib = new(FVoxelMm))
        {
            Voxels voxBeam = Voxels.voxLatticeBeam(
                oLib, new Vector3(0, 0, 0), FRadiusMm, new Vector3(0, 0, FLengthMm), FRadiusMm);
            voxBeam.CalculateProperties(out float _, out BBox3 oBBox);
            oBox = oBBox;
        }

        Vector3 vecSize = oBox.vecSize();
        float fTol = 3 * FVoxelMm;

        oOutput.WriteLine(
            $"bbox {vecSize.X:F3} x {vecSize.Y:F3} x {vecSize.Z:F3} mm, "
            + $"Z {oBox.vecMin.Z:F3} to {oBox.vecMax.Z:F3}");

        Assert.Equal(2 * FRadiusMm, vecSize.X, fTol);
        Assert.Equal(2 * FRadiusMm, vecSize.Y, fTol);
        Assert.Equal(FLengthMm + 2 * FRadiusMm, vecSize.Z, fTol);
        Assert.Equal(-FRadiusMm, oBox.vecMin.Z, fTol);
    }
}
