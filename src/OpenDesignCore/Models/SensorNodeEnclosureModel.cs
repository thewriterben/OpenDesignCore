using System.Numerics;
using OpenDesignCore.Data;
using PicoGK;

namespace OpenDesignCore.Models;

/// <summary>Which half of the enclosure, and in which pose.</summary>
public enum EnclosurePart { Base, Lid }

/// <summary>
/// Inputs to <see cref="SensorNodeEnclosureModel"/>. Everything the board says
/// about itself (outline, thickness, where the headers, the BME280, the module
/// and its antenna are) is read from its OpenPartsCore features, not from
/// here. What is here is either the enclosure's own design choice or a cited
/// fastener dimension; the run copies each into the sidecar with its source.
/// </summary>
public sealed record SensorNodeEnclosureParams
{
    /// <summary>M3 hole centres in the features frame (KiCad position with y negated), mm.</summary>
    public required IReadOnlyList<Vector2> HolesMm { get; init; }

    /// <summary>Gap between board edge and cavity wall, per side.</summary>
    public required float EdgeClearanceMm { get; init; }
    public required float WallMm { get; init; }
    /// <summary>Wall, floor and lid-top thickness over the antenna zone.</summary>
    public required float ThinWallMm { get; init; }
    public required float FloorMm { get; init; }
    /// <summary>Board underside above the floor's top face.</summary>
    public required float StandoffMm { get; init; }
    /// <summary>Lid ceiling above the board's component face.</summary>
    public required float CeilingAboveBoardMm { get; init; }
    public required float LidTopMm { get; init; }
    /// <summary>Gap between the base wall top and the lid wall bottom, so the screws clamp the board and not the walls.</summary>
    public required float SeamGapMm { get; init; }

    public required float BossDiameterMm { get; init; }
    public required float ClearanceHoleMm { get; init; }
    public required float NutAcrossFlatsMm { get; init; }
    public required float NutThicknessMm { get; init; }
    /// <summary>Added to the nut's across-flats for the pocket (total, not per side).</summary>
    public required float NutPocketClearanceMm { get; init; }
    public required float ScrewHeadDiameterMm { get; init; }
    /// <summary>Nominal length under the head of the screw the stack is cut for.</summary>
    public required float ScrewLengthMm { get; init; }

    /// <summary>Per side, around each header's own outline.</summary>
    public required float HeaderWindowMarginMm { get; init; }
    public required float VentWidthMm { get; init; }
    public required float BaffleMm { get; init; }
    /// <summary>Gap between the baffle's lower edge and the board face.</summary>
    public required float BaffleGapMm { get; init; }

    /// <summary>The defaults the run uses; sources are in <see cref="SensorNodeEnclosureRun"/>.</summary>
    public static SensorNodeEnclosureParams ODefault() => new()
    {
        HolesMm = [new(3.5f, -17f), new(40.5f, -17f), new(3.5f, -48.5f), new(40.5f, -48.5f)],
        EdgeClearanceMm = 0.5f,
        WallMm = 2.0f,
        ThinWallMm = 1.2f,
        FloorMm = 3.6f,
        StandoffMm = 5.0f,
        CeilingAboveBoardMm = 4.4f,
        LidTopMm = 2.0f,
        SeamGapMm = 0.2f,
        BossDiameterMm = 6.5f,
        ClearanceHoleMm = 3.4f,
        NutAcrossFlatsMm = 5.5f,
        NutThicknessMm = 2.4f,
        NutPocketClearanceMm = 0.4f,
        ScrewHeadDiameterMm = 5.5f,
        ScrewLengthMm = 16f,
        HeaderWindowMarginMm = 0.4f,
        VentWidthMm = 1.2f,
        BaffleMm = 1.2f,
        BaffleGapMm = 0.6f,
    };
}

/// <summary>
/// Where the board's own features put things, read once from the registry.
/// Frame: the features frame (OpenPartsCore boards/opencircuitcore-sensor-node),
/// which is also the enclosure's assembled frame: board underside at z = 0.
/// </summary>
public sealed record SensorNodeLayout
{
    public required Vector3 BoardMin { get; init; }
    public required Vector3 BoardMax { get; init; }
    public required (Vector3 Min, Vector3 Max) J1 { get; init; }
    public required (Vector3 Min, Vector3 Max) J2 { get; init; }
    public required (Vector3 Min, Vector3 Max) U1 { get; init; }
    public required (Vector3 Min, Vector3 Max) U2 { get; init; }
    /// <summary>The antenna zone is every y at or above this.</summary>
    public required float AntennaZoneYMm { get; init; }
    /// <summary>Highest y of anything on the board between the module and the header end; the baffle goes between it and the module.</summary>
    public required float SensorRowTopYMm { get; init; }
    /// <summary>Lowest z of anything under the board (header pin tails).</summary>
    public required float UndersideMinZMm { get; init; }
    /// <summary>Highest z of anything the lid must clear except the header pins, which pass through their windows.</summary>
    public required float TallestUnderLidZMm { get; init; }

    public static SensorNodeLayout OFrom(PartFeatures oF)
    {
        (Vector3, Vector3) Box(string strId)
        {
            PartFeature? o = oF.Items.SingleOrDefault(i => i.Id == strId)
                ?? throw new ArgumentException(
                    $"{SensorNodeEnclosureModel.StrModelId}: {oF.PartId} has no item '{strId}', "
                    + "and the enclosure is cut around it. Refusing rather than guessing where it is.");
            (double[] a, double[] b) = o.OBounds();
            return (new((float)a[0], (float)a[1], (float)a[2]), new((float)b[0], (float)b[1], (float)b[2]));
        }

        (Vector3 vecBMin, Vector3 vecBMax) = Box("Board");
        (Vector3, Vector3) oU1 = Box("U1");
        (Vector3, Vector3) oAnt = Box("U1-ANT");
        float fRowTop = oF.Items
            .Where(i => i.Kind != "board" && i.Id != "J1" && i.Id != "J2")
            .Select(i => i.OBounds())
            .Where(b => b.Max[1] < oU1.Item1.Y)
            .Max(b => (float)b.Max[1]);
        float fUnder = oF.Items.Min(i => (float)i.OBounds().Min[2]);
        float fTall = oF.Items
            .Where(i => i.Id != "J1" && i.Id != "J2")
            .Max(i => (float)i.OBounds().Max[2]);

        return new SensorNodeLayout
        {
            BoardMin = vecBMin,
            BoardMax = vecBMax,
            J1 = Box("J1"),
            J2 = Box("J2"),
            U1 = oU1,
            U2 = Box("U2"),
            AntennaZoneYMm = oAnt.Item1.Y,
            SensorRowTopYMm = fRowTop,
            UndersideMinZMm = fUnder,
            TallestUnderLidZMm = fTall,
        };
    }
}

/// <summary>
/// A two-part screwed enclosure for the OpenCircuitCore sensor-node.
///
/// Stack, in the assembled (features) frame, board underside at z = 0: a base
/// with a floor and four bosses the board sits on; a lid with four posts that
/// press on the board's top face; one M3 socket-head screw per hole from the
/// lid top, through post, board and boss, into a hex nut trapped in the floor.
/// The screws clamp the board between boss and post; the walls stop
/// <see cref="SensorNodeEnclosureParams.SeamGapMm"/> short of each other so
/// they never take the clamp load.
///
/// What the brief asked for, and where it is:
/// - header access: a window through the lid top over J1 and J2, the header's
///   own outline plus a margin; the pins stand through it and a socket
///   housing goes down onto the insulator. Checked as an access volume.
/// - BME280 in ambient air: a full-width baffle between the module and the
///   sensor row, so the sensor sits in its own chamber with slots in the lid
///   top over it and in both side walls; the module chamber is vented
///   separately.
/// - antenna: every wall, the floor and the lid top over the antenna zone are
///   thinned to <see cref="SensorNodeEnclosureParams.ThinWallMm"/> from the
///   inside, and no screw, nut, boss or post reaches into the zone.
///
/// Subtraction and union of boxes and clipped beams only. Print poses: the
/// base floor-down, the lid top-down (rotated 180 degrees about x).
/// </summary>
public static class SensorNodeEnclosureModel
{
    public const string StrModelId = "sensor-node-enclosure/0.1";
    public const string StrPartId = "boards/opencircuitcore-sensor-node";

    /// <summary>
    /// Every designed gap or web spans at least two voxels: the thin wall, a
    /// vent, the edge clearance, and the nut pocket's clearance per side.
    /// </summary>
    public static float FResolutionFloorMm(SensorNodeEnclosureParams p) => new[]
    {
        p.ThinWallMm, p.VentWidthMm, p.BaffleMm, p.EdgeClearanceMm, p.SeamGapMm, p.NutPocketClearanceMm / 2f,
    }.Min() / 2f;

    // ── the stack, assembled frame ────────────────────────────────────────

    public static float FBaseBottomZ(SensorNodeEnclosureParams p) => -(p.StandoffMm + p.FloorMm);
    public static float FFloorTopZ(SensorNodeEnclosureParams p) => -p.StandoffMm;
    public static float FBaseTopZ(SensorNodeEnclosureParams p, SensorNodeLayout l) => l.BoardMax.Z - p.SeamGapMm;
    public static float FLidBottomZ(SensorNodeLayout l) => l.BoardMax.Z;
    public static float FCeilingZ(SensorNodeEnclosureParams p, SensorNodeLayout l) => l.BoardMax.Z + p.CeilingAboveBoardMm;
    public static float FLidTopZ(SensorNodeEnclosureParams p, SensorNodeLayout l) => FCeilingZ(p, l) + p.LidTopMm;
    /// <summary>Where the screw tip lands with its head on the lid top.</summary>
    public static float FScrewTipZ(SensorNodeEnclosureParams p, SensorNodeLayout l) => FLidTopZ(p, l) - p.ScrewLengthMm;
    /// <summary>Nut pocket from the base underside up to here; the screw pulls the nut against its roof.</summary>
    public static float FNutPocketTopZ(SensorNodeEnclosureParams p) => FBaseBottomZ(p) + p.NutThicknessMm + 0.8f;
    public static float FBaffleY(SensorNodeLayout l) => (l.U1.Min.Y + l.SensorRowTopYMm) / 2f;

    public static (Vector3 Min, Vector3 Max) OOuter(SensorNodeEnclosureParams p, SensorNodeLayout l) =>
        (new(l.BoardMin.X - p.EdgeClearanceMm - p.WallMm, l.BoardMin.Y - p.EdgeClearanceMm - p.WallMm, FBaseBottomZ(p)),
         new(l.BoardMax.X + p.EdgeClearanceMm + p.WallMm, l.BoardMax.Y + p.EdgeClearanceMm + p.ThinWallMm, FLidTopZ(p, l)));

    /// <summary>
    /// Parameter checks that need no geometry. Each one is a way the enclosure
    /// would fit its own numbers and still fail the board or the brief.
    /// </summary>
    public static void ValidateParams(SensorNodeEnclosureParams p, SensorNodeLayout l)
    {
        float[] aPositive =
        [
            p.EdgeClearanceMm, p.WallMm, p.ThinWallMm, p.FloorMm, p.StandoffMm, p.CeilingAboveBoardMm,
            p.LidTopMm, p.SeamGapMm, p.BossDiameterMm, p.ClearanceHoleMm, p.NutAcrossFlatsMm,
            p.NutThicknessMm, p.NutPocketClearanceMm, p.ScrewHeadDiameterMm, p.ScrewLengthMm,
            p.HeaderWindowMarginMm, p.VentWidthMm, p.BaffleMm, p.BaffleGapMm,
        ];
        if (aPositive.Any(f => !(f > 0)))
            throw new ArgumentException($"{StrModelId}: every length must be > 0.");
        if (p.ThinWallMm >= p.WallMm || p.ThinWallMm >= p.LidTopMm || p.ThinWallMm >= p.FloorMm)
            throw new ArgumentException($"{StrModelId}: ThinWallMm must be thinner than the wall, floor and lid top it thins.");
        if (p.HolesMm.Count == 0)
            throw new ArgumentException($"{StrModelId}: no mounting holes.");

        string Fmt(float f) => f.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        // Under the board: the header pin tails must clear the floor.
        if (-p.StandoffMm >= l.UndersideMinZMm)
            throw new ArgumentException(
                $"{StrModelId}: the floor top at {Fmt(-p.StandoffMm)} mm meets the board's underside "
                + $"parts, which reach {Fmt(l.UndersideMinZMm)} mm.");
        // Over the board: the ceiling must clear everything but the pins.
        if (FCeilingZ(p, l) <= l.TallestUnderLidZMm)
            throw new ArgumentException(
                $"{StrModelId}: the ceiling at {Fmt(FCeilingZ(p, l))} mm does not clear the tallest "
                + $"part under the lid at {Fmt(l.TallestUnderLidZMm)} mm.");

        // The screw: full nut engagement, and no tip proud of the underside.
        float fTip = FScrewTipZ(p, l);
        float fNutBottom = FNutPocketTopZ(p) - p.NutThicknessMm;
        if (fTip > fNutBottom)
            throw new ArgumentException(
                $"{StrModelId}: an M3x{Fmt(p.ScrewLengthMm)} tip lands at {Fmt(fTip)} mm, short of the "
                + $"nut's far face at {Fmt(fNutBottom)} mm: the nut would not be fully engaged.");
        if (fTip < FBaseBottomZ(p))
            throw new ArgumentException(
                $"{StrModelId}: an M3x{Fmt(p.ScrewLengthMm)} tip lands at {Fmt(fTip)} mm, below the "
                + $"base underside at {Fmt(FBaseBottomZ(p))} mm: it would stand proud of the table.");
        if (FNutPocketTopZ(p) >= FFloorTopZ(p))
            throw new ArgumentException($"{StrModelId}: the nut pocket breaks through the floor.");

        // No metal in the antenna zone: nut corners and screw heads stay out.
        float fNutCornerR = (p.NutAcrossFlatsMm + p.NutPocketClearanceMm) / MathF.Sqrt(3f);
        float fMetalR = MathF.Max(fNutCornerR, p.ScrewHeadDiameterMm / 2f);
        float fFixtureR = MathF.Max(fMetalR, p.BossDiameterMm / 2f);
        foreach (Vector2 h in p.HolesMm)
        {
            if (h.Y + fFixtureR >= l.AntennaZoneYMm)
                throw new ArgumentException(
                    $"{StrModelId}: the fastener at ({Fmt(h.X)}, {Fmt(h.Y)}) reaches y = "
                    + $"{Fmt(h.Y + fFixtureR)} mm, into the antenna zone (y >= {Fmt(l.AntennaZoneYMm)}). "
                    + "No metal and no boss or post over the antenna.");
        }
    }

    // ── building ──────────────────────────────────────────────────────────

    /// <summary>
    /// Boxes, clipped z-cylinders and hex prisms in the assembled frame, written
    /// into whichever pose the part is wanted in. Every pose is a proper rigid
    /// motion that keeps the z axis a z axis, so boxes stay boxes.
    /// </summary>
    private sealed class Pen(Library oLib, Func<Vector3, Vector3> fnMap)
    {
        public Voxels Box(Vector3 a, Vector3 b)
        {
            Vector3 u = fnMap(a), v = fnMap(b);
            Vector3 vecMin = Vector3.Min(u, v), vecMax = Vector3.Max(u, v);
            return new Voxels(Utils.mshCreateCube(oLib, vecMax - vecMin, (vecMin + vecMax) / 2f));
        }

        /// <summary>A flat-ended cylinder: a beam clipped to its span (the beam's caps are hemispheres).</summary>
        public Voxels CylZ(float x, float y, float z0, float z1, float r)
        {
            Voxels voxBeam = Voxels.voxLatticeBeam(
                oLib, fnMap(new(x, y, z0 - r)), r, fnMap(new(x, y, z1 + r)), r);
            return voxBeam.voxBoolIntersect(Box(new(x - r - 1, y - r - 1, z0), new(x + r + 1, y + r + 1, z1)));
        }

        /// <summary>Hexagonal prism, flats parallel to x, across-flats <paramref name="fAf"/>.</summary>
        public Voxels HexZ(float x, float y, float z0, float z1, float fAf)
        {
            float fR = fAf / MathF.Sqrt(3f);
            Vector3[] aLo = new Vector3[6], aHi = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float fA = MathF.PI / 3f * i;
                aLo[i] = fnMap(new(x + fR * MathF.Cos(fA), y + fR * MathF.Sin(fA), z0));
                aHi[i] = fnMap(new(x + fR * MathF.Cos(fA), y + fR * MathF.Sin(fA), z1));
            }
            Vector3 vecLoC = fnMap(new(x, y, z0)), vecHiC = fnMap(new(x, y, z1));
            Mesh msh = new(oLib);
            for (int i = 0; i < 6; i++)
            {
                int j = (i + 1) % 6;
                // Counter-clockwise seen from outside (unmapped frame; the
                // pose is a proper rotation, so orientation survives it).
                msh.nAddTriangle(vecHiC, aHi[i], aHi[j]);
                msh.nAddTriangle(vecLoC, aLo[j], aLo[i]);
                msh.nAddTriangle(aLo[i], aLo[j], aHi[j]);
                msh.nAddTriangle(aLo[i], aHi[j], aHi[i]);
            }
            return new Voxels(msh);
        }
    }

    /// <summary>The nut pocket's hex prism on its own, unposed, so its mesh winding can be checked by volume.</summary>
    public static Voxels VoxHexPrismZ(Library oLib, float x, float y, float z0, float z1, float fAcrossFlatsMm) =>
        new Pen(oLib, v => v).HexZ(x, y, z0, z1, fAcrossFlatsMm);

    /// <summary>Assembled frame to print frame: the base floor-down, the lid rotated 180 degrees about x, top on the bed.</summary>
    public static Func<Vector3, Vector3> FnPose(EnclosurePart e, bool bPrintPose, SensorNodeEnclosureParams p, SensorNodeLayout l)
    {
        if (!bPrintPose)
            return v => v;
        float fBottom = FBaseBottomZ(p), fTop = FLidTopZ(p, l);
        return e == EnclosurePart.Base
            ? v => new Vector3(v.X, v.Y, v.Z - fBottom)
            : v => new Vector3(v.X, -v.Y, fTop - v.Z);
    }

    public static Voxels VoxBuild(
        Library oLib, SensorNodeEnclosureParams p, SensorNodeLayout l,
        EnclosurePart e, bool bPrintPose, float fVoxelSizeMm)
    {
        ValidateParams(p, l);
        float fFloor = FResolutionFloorMm(p);
        if (fVoxelSizeMm > fFloor)
        {
            throw new ResolutionFloorException(
                $"{StrModelId}: voxel size {fVoxelSizeMm} mm exceeds the resolution floor {fFloor:F4} mm. "
                + "The thin antenna wall, the vents, the edge gap and the nut pocket's clearance must "
                + "each span two voxels or they close up or thin out silently; refusing (ADR-0003).");
        }

        Pen o = new(oLib, FnPose(e, bPrintPose, p, l));
        return e == EnclosurePart.Base ? VoxBase(o, p, l, fVoxelSizeMm) : VoxLid(o, p, l, fVoxelSizeMm);
    }

    private static Voxels VoxBase(Pen o, SensorNodeEnclosureParams p, SensorNodeLayout l, float fVoxel)
    {
        float fOver = 2 * fVoxel + 0.5f;
        (Vector3 vecOMin, Vector3 vecOMax) = OOuter(p, l);
        float fZ0 = FBaseBottomZ(p), fZf = FFloorTopZ(p), fZt = FBaseTopZ(p, l);
        Vector3 vecIMin = new(l.BoardMin.X - p.EdgeClearanceMm, l.BoardMin.Y - p.EdgeClearanceMm, fZf);
        Vector3 vecIMax = new(l.BoardMax.X + p.EdgeClearanceMm, l.BoardMax.Y + p.EdgeClearanceMm, fZt + fOver);

        Voxels vox = o.Box(vecOMin, vecOMax with { Z = fZt });
        vox -= o.Box(vecIMin, vecIMax);
        // Antenna zone: walls and floor thinned from the inside.
        vox -= o.Box(
            new(vecOMin.X + p.ThinWallMm, l.AntennaZoneYMm, fZ0 + p.ThinWallMm),
            new(vecOMax.X - p.ThinWallMm, vecIMax.Y, vecIMax.Z));

        foreach (Vector2 h in p.HolesMm)
            vox += o.CylZ(h.X, h.Y, fZf - 0.5f, 0f, p.BossDiameterMm / 2f);
        foreach (Vector2 h in p.HolesMm)
        {
            vox -= o.CylZ(h.X, h.Y, fZ0 - fOver, fZt + fOver, p.ClearanceHoleMm / 2f);
            vox -= o.HexZ(h.X, h.Y, fZ0 - fOver, FNutPocketTopZ(p), p.NutAcrossFlatsMm + p.NutPocketClearanceMm);
        }
        return vox;
    }

    private static Voxels VoxLid(Pen o, SensorNodeEnclosureParams p, SensorNodeLayout l, float fVoxel)
    {
        float fOver = 2 * fVoxel + 0.5f;
        (Vector3 vecOMin, Vector3 vecOMax) = OOuter(p, l);
        float fZb = FLidBottomZ(l), fZc = FCeilingZ(p, l), fZt = FLidTopZ(p, l);
        float fIx0 = l.BoardMin.X - p.EdgeClearanceMm, fIx1 = l.BoardMax.X + p.EdgeClearanceMm;
        float fIy0 = l.BoardMin.Y - p.EdgeClearanceMm, fIy1 = l.BoardMax.Y + p.EdgeClearanceMm;

        Voxels vox = o.Box(vecOMin with { Z = fZb }, vecOMax);
        vox -= o.Box(new(fIx0, fIy0, fZb - fOver), new(fIx1, fIy1, fZc));
        // Antenna zone: walls and lid top thinned from the inside.
        vox -= o.Box(
            new(vecOMin.X + p.ThinWallMm, l.AntennaZoneYMm, fZb - fOver),
            new(vecOMax.X - p.ThinWallMm, fIy1, fZt - p.ThinWallMm));

        // Posts down to the board face, and the baffle across the cavity.
        foreach (Vector2 h in p.HolesMm)
            vox += o.CylZ(h.X, h.Y, fZb, fZc + 0.5f, p.BossDiameterMm / 2f);
        float fBy = FBaffleY(l);
        vox += o.Box(
            new(fIx0 - 0.5f, fBy - p.BaffleMm / 2f, l.BoardMax.Z + p.BaffleGapMm),
            new(fIx1 + 0.5f, fBy + p.BaffleMm / 2f, fZc + 0.5f));

        foreach (Vector2 h in p.HolesMm)
            vox -= o.CylZ(h.X, h.Y, fZb - fOver, fZt + fOver, p.ClearanceHoleMm / 2f);

        // Header windows through the lid top, the full lid height.
        foreach ((Vector3 a, Vector3 b) in new[] { l.J1, l.J2 })
        {
            float m = p.HeaderWindowMarginMm;
            vox -= o.Box(new(a.X - m, a.Y - m, fZb - fOver), new(b.X + m, b.Y + m, fZt + fOver));
        }

        foreach ((Vector3 a, Vector3 b) in AVentSlots(p, l))
            vox -= o.Box(a, b);
        return vox;
    }

    /// <summary>
    /// The vent slots, as boxes in the assembled frame: three through the lid
    /// top over the BME280, kept a web's width off the baffle; four in each
    /// side wall of the sensor chamber; five through the lid top over the
    /// module body, outside the antenna zone.
    /// </summary>
    public static IReadOnlyList<(Vector3 Min, Vector3 Max)> AVentSlots(SensorNodeEnclosureParams p, SensorNodeLayout l)
    {
        const float fOver = 1.0f;
        float w = p.VentWidthMm, fPitch = 2 * w;
        float fZc = FCeilingZ(p, l), fZt = FLidTopZ(p, l);
        (Vector3 vecOMin, Vector3 vecOMax) = OOuter(p, l);
        float fIx0 = l.BoardMin.X - p.EdgeClearanceMm, fIx1 = l.BoardMax.X + p.EdgeClearanceMm;
        float fBaffleLow = FBaffleY(l) - p.BaffleMm / 2f;
        List<(Vector3, Vector3)> a = [];

        // Over the BME280: slots along y, centred on it unless that would run into the baffle.
        const float fSensorSlotLen = 5.0f;
        float fCx = (l.U2.Min.X + l.U2.Max.X) / 2f;
        float fCy = MathF.Min((l.U2.Min.Y + l.U2.Max.Y) / 2f, fBaffleLow - p.WallMm - fSensorSlotLen / 2f);
        for (int i = -1; i <= 1; i++)
        {
            float x = fCx + i * fPitch;
            a.Add((new(x - w / 2, fCy - fSensorSlotLen / 2, fZc - 0.1f), new(x + w / 2, fCy + fSensorSlotLen / 2, fZt + fOver)));
        }

        // Sensor-chamber side walls: vertical slots, both sides, so air crosses the chamber.
        float fZ0 = l.BoardMax.Z + 0.8f, fZ1 = fZc - 0.8f;
        for (int i = 0; i < 4; i++)
        {
            float y = fBaffleLow - 2.5f - 3.0f * i;
            a.Add((new(vecOMin.X - fOver, y - w / 2, fZ0), new(fIx0 + 0.1f, y + w / 2, fZ1)));
            a.Add((new(fIx1 - 0.1f, y - w / 2, fZ0), new(vecOMax.X + fOver, y + w / 2, fZ1)));
        }

        // Over the module body, outside the antenna zone, so its heat leaves
        // the module chamber instead of soaking the enclosure.
        float fMy0 = l.U1.Min.Y + 2.0f, fMy1 = l.AntennaZoneYMm - 2.0f;
        float fMx = (l.U1.Min.X + l.U1.Max.X) / 2f;
        for (int i = -2; i <= 2; i++)
        {
            float x = fMx + i * 2.5f * w;
            a.Add((new(x - w / 2, fMy0, fZc - 0.1f), new(x + w / 2, fMy1, fZt + fOver)));
        }
        return a;
    }

    /// <summary>
    /// Validation gate: non-empty, and the bounding box of the part, in the
    /// pose it was built in, matches the stack within two voxels.
    /// </summary>
    public static void Validate(
        Voxels vox, SensorNodeEnclosureParams p, SensorNodeLayout l, EnclosurePart e, float fVoxelSizeMm)
    {
        if (vox.bIsEmpty())
            throw new GeometryValidationException($"{StrModelId} {e}: result voxel field is empty.");
        (Vector3 vecOMin, Vector3 vecOMax) = OOuter(p, l);
        float fH = e == EnclosurePart.Base
            ? FBaseTopZ(p, l) - FBaseBottomZ(p)
            : FLidTopZ(p, l) - FLidBottomZ(l);
        Vector3 vecWant = new(vecOMax.X - vecOMin.X, vecOMax.Y - vecOMin.Y, fH);
        Vector3 vecGot = vox.oCalculateBoundingBox().vecSize();
        float fTol = 2 * fVoxelSizeMm;
        if (Math.Abs(vecGot.X - vecWant.X) > fTol || Math.Abs(vecGot.Y - vecWant.Y) > fTol
            || Math.Abs(vecGot.Z - vecWant.Z) > fTol)
        {
            throw new GeometryValidationException(
                $"{StrModelId} {e}: bounding box {vecGot.X:F2} x {vecGot.Y:F2} x {vecGot.Z:F2} mm deviates "
                + $"from {vecWant.X:F2} x {vecWant.Y:F2} x {vecWant.Z:F2} mm by more than {fTol:F2} mm.");
        }
    }
}
