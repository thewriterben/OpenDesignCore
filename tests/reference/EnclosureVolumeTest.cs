using OpenDesignCore.Data;
using OpenDesignCore.Models;
using PicoGK;
using Xunit;
using Xunit.Abstractions;

namespace OpenDesignCore.Tests.Reference;

/// <summary>
/// Reference case: the enclosure tray's voxel-derived volume against its exact
/// analytic volume.
///
/// <para>
/// The tray is a rectangular solid minus a rectangular cavity. Its volume has a
/// closed form in the model's own parameters, so the gap between that and what
/// <see cref="Voxels.CalculateProperties"/> reports is discretization error and
/// nothing else — there is no modelling approximation in a box. Every other
/// test in this repo checks that a number did not change; this one checks that
/// a number is right.
/// </para>
/// </summary>
public sealed class EnclosureVolumeTest(ITestOutputHelper oOutput)
{
    /// <summary>
    /// Exact volume of the tray: outer solid minus the part of the cavity that
    /// lies inside it. The cavity overshoots the outer top to leave the tray
    /// open, so the intersected cavity height is (outerZ − wall), not the
    /// cavity's own height.
    /// </summary>
    private static double FExactVolumeCubicMm(EnclosureShellParams oParams)
    {
        double fEx = oParams.Envelope.X, fEy = oParams.Envelope.Y, fEz = oParams.Envelope.Z;
        double fC = oParams.ClearanceMm, fW = oParams.WallMm;

        double fOuter = (fEx + 2 * fC + 2 * fW) * (fEy + 2 * fC + 2 * fW) * (fW + fEz + fC);
        double fCavity = (fEx + 2 * fC) * (fEy + 2 * fC) * (fEz + fC);
        return fOuter - fCavity;
    }

    /// <summary>
    /// Exact surface area of the same solid — outer bottom and sides, the rim,
    /// the cavity floor and the cavity walls. Used only to size the error
    /// bound: discretization error lives on the surface, so a bound that
    /// ignored area would be a constant in disguise.
    /// </summary>
    private static double FExactSurfaceAreaSqMm(EnclosureShellParams oParams)
    {
        double fEx = oParams.Envelope.X, fEy = oParams.Envelope.Y, fEz = oParams.Envelope.Z;
        double fC = oParams.ClearanceMm, fW = oParams.WallMm;

        double fOx = fEx + 2 * fC + 2 * fW, fOy = fEy + 2 * fC + 2 * fW, fOz = fW + fEz + fC;
        double fCx = fEx + 2 * fC, fCy = fEy + 2 * fC;

        // Outer bottom + rim + cavity floor collapse to 2·Ox·Oy; the rim is
        // (Ox·Oy − Cx·Cy) and the cavity floor is Cx·Cy.
        return (2 * fOx * fOy)
             + (2 * (fOx + fOy) * fOz)
             + (2 * (fCx + fCy) * (fOz - fW));
    }

    [Fact]
    public void VoxelVolumeMatchesAnalyticVolumeWithinOneVoxelOfSurface()
    {
        // Dimensions are arbitrary but unequal on every axis: equal edges would
        // let a transposed term in the closed form pass unnoticed, the same
        // reasoning that made the calibration block 20 × 30 × 15 rather than a
        // cube (ADR-0012 context).
        EnclosureShellParams oParams = new()
        {
            Envelope = new EnvelopeMm { X = 18.0, Y = 25.5, Z = 3.1 },
            ClearanceMm = 0.30f,
            WallMm = 2.40f,
        };
        const float fVoxelSizeMm = 0.10f;

        double fExact = FExactVolumeCubicMm(oParams);
        double fArea = FExactSurfaceAreaSqMm(oParams);

        // The bound, stated as an argument rather than a number: a
        // signed-distance field places the interface within a band around the
        // true surface, so the worst the representation permits is being wrong
        // by about one voxel of material everywhere on that surface. Scales
        // with area and voxel size, both of which are inputs — no epsilon.
        double fBound = fArea * fVoxelSizeMm;

        double fMeasured;
        using (Library oLib = new(fVoxelSizeMm))
        {
            Voxels voxShell = EnclosureShellModel.VoxBuild(oLib, oParams, fVoxelSizeMm);
            voxShell.CalculateProperties(out float fVolume, out BBox3 _);
            fMeasured = fVolume;
        }

        double fError = Math.Abs(fMeasured - fExact);

        // Reported on every run, pass or fail. A ceiling that is never compared
        // against the real figure stays loose forever; this is what makes
        // tightening it later a deliberate act rather than a guess.
        oOutput.WriteLine(
            $"analytic {fExact:F3} mm³, voxel {fMeasured:F3} mm³, "
            + $"error {fError:F3} mm³ ({fError / fExact * 100:F3} %), "
            + $"bound {fBound:F3} mm³ (area {fArea:F1} mm² × voxel {fVoxelSizeMm} mm)");

        Assert.True(
            fError <= fBound,
            $"Voxel volume {fMeasured:F3} mm³ differs from the analytic {fExact:F3} mm³ by "
            + $"{fError:F3} mm³, beyond the {fBound:F3} mm³ a one-voxel band over "
            + $"{fArea:F1} mm² of surface permits. That is not discretization — it is a "
            + "geometry or measurement defect.");
    }
}
