using OpenDesignCore.Verification;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// The calibration block's nominals: 40 x 60, shelf at 4, tall face at 25.
/// Unequal on purpose, so a transposition is detectable. These tests are what
/// makes it actually detected.
/// </summary>
public sealed class MeasurementAssignmentTests
{
    private static List<Reading> AFour(double fX, double fY, double fZLow, double fZHigh)
        => [
            new("X", 40, fX),
            new("Y", 60, fY),
            new("Z-low", 4, fZLow),
            new("Z-high", 25, fZHigh),
        ];

    [Fact]
    public void PlausibleReadingsPass()
    {
        MeasurementAssignment.Assert(AFour(39.98, 59.97, 4.01, 25.02));
    }

    [Fact]
    public void ReadingsExactlyOnNominalPass()
    {
        // Zero deviation everywhere. The check compares distances, and a tie
        // against 0 must not be read as "closer to something else".
        MeasurementAssignment.Assert(AFour(40, 60, 4, 25));
    }

    [Fact]
    public void AZLowZHighSwapIsRefusedAndNamesBothEnds()
    {
        // The one this was built for. Both are "bed to a flat face", the same
        // gesture twice, and the two arguments sit next to each other on the
        // command line.
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => MeasurementAssignment.Assert(AFour(39.98, 59.97, 25.02, 4.01)));

        // The partner must be the NEAREST nominal, not just any nominal that
        // beats its own. Running this for real first reported "check Z-low
        // against X" — true (25.02 is nearer 40 than 4) and useless, because
        // it is nearer 25 than either. A refusal that misdirects sends someone
        // to re-measure the wrong face.
        Assert.Contains("check Z-low against Z-high", oEx.Message);
        Assert.DoesNotContain("against X", oEx.Message);
        Assert.Contains("SHELF", oEx.Message);
        Assert.Contains("Nothing is recorded", oEx.Message);
    }

    [Fact]
    public void AnXYSwapIsRefused()
    {
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => MeasurementAssignment.Assert(AFour(59.97, 39.98, 4.01, 25.02)));
        Assert.Contains("X reading of 59.97", oEx.Message);
        Assert.Contains("Y nominal", oEx.Message);
    }

    [Fact]
    public void TheThreeValueFormIsCheckedToo()
    {
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => MeasurementAssignment.Assert([
                new("X", 40, 24.9),
                new("Y", 60, 59.97),
                new("Z", 25, 39.98),
            ]));
        Assert.Contains("X reading", oEx.Message);
    }

    [Fact]
    public void AGenuineDeviationIsNowhereNearTheThreshold()
    {
        // The reason no tolerance is declared. A 2 % error — already far worse
        // than any real shrinkage — leaves 40 at 40.8, still 19 mm from the
        // nearest other nominal. Real readings and transposed readings are not
        // close to each other, so there is no line to draw.
        MeasurementAssignment.Assert(AFour(40.8, 61.2, 4.08, 25.5));
        MeasurementAssignment.Assert(AFour(39.2, 58.8, 3.92, 24.5));
    }

    [Fact]
    public void AnExactlyEquidistantReadingIsRefusedRatherThanGuessed()
    {
        // 14.5 is exactly halfway between the 4 and 25 nominals — 10.5 mm from
        // each. This test was first written claiming it was "nearer 25", which
        // was simply wrong, and it failed: the tie fell through a `>=` and
        // passed. The fixture was wrong AND the check was wrong, which is the
        // only reason it turned up.
        //
        // An equidistant reading is the most ambiguous value there is, so it
        // is refused. Not because 14.5 is a plausible mistake, but because a
        // rule that says "refuse the ambiguous" cannot make an exception for
        // the maximally ambiguous case.
        Assert.Throws<ArgumentException>(
            () => MeasurementAssignment.Assert(AFour(39.98, 59.97, 14.5, 25.02)));
    }

    [Fact]
    public void AReadingNearerAnotherNominalIsRefused()
    {
        // 16 against a 4 mm nominal: 12 away from its own, 9 from the 25.
        Assert.Throws<ArgumentException>(
            () => MeasurementAssignment.Assert(AFour(39.98, 59.97, 16, 25.02)));
    }

    [Fact]
    public void AmbiguousNominalsRefuseRatherThanPretendToResolve()
    {
        // A design whose nominals are 20 and 20.4 cannot have its readings
        // told apart by any means, including this one. Refusing is still the
        // right answer: the fault is the design, and recording a reading as
        // though it were assignable is the error being prevented.
        Assert.Throws<ArgumentException>(
            () => MeasurementAssignment.Assert([
                new("X", 20, 20.3),
                new("Y", 20.4, 20.1),
            ]));
    }
}
