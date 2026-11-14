using System;
using System.Linq;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Wind;

public sealed record SquareState(double Yard, HullPoint ArmPort, HullPoint ArmStarboard, HullPoint ClewPort, HullPoint ClewStarboard, SailForm Form, double SheetNewtons)
    : RigState(Yard, 0, Form, SheetNewtons);

/// <summary>A square sail on a yard braced about the mast, sheeted at its lee clew.</summary>
public sealed class SquareRig : Rigging
{
    public const string Kind = "square";

    private const double ShortestLeverMetres = 1e-3;

    private readonly HullPoint heel;
    private readonly HullPoint masthead;
    private readonly HullPoint axis;
    private readonly (HullPoint Port, HullPoint Starboard) arms;
    private readonly (HullPoint Port, HullPoint Starboard) heads;
    private readonly (HullPoint Port, HullPoint Starboard) clews;
    private readonly (HullPoint Port, HullPoint Starboard) leads;
    private readonly (HullPoint From, HullPoint To)[] stays;
    private readonly SailForm rest;
    private readonly (double Yard, double Cloth) kilograms;
    private static readonly HullPoint Gravity = RoughSeas.Water.WaveTheory.Gravity * new HullPoint(0, -1, 0);

    private SquareRig(JsonObject rig, double camber, (double Yard, double Cloth) kilograms, RigWidths widths)
    {
        HullPoint At(string name) => HullPoint.Of(rig[name + "Metres"]);
        (heel, masthead, Widths, this.kilograms) = (At("mastHeel"), At("masthead"), widths, kilograms);
        axis = (masthead - heel).Unit;
        arms = (At("yardPort"), At("yardStarboard"));
        heads = (At("headPort"), At("headStarboard"));
        clews = (At("clewPort"), At("clewStarboard"));
        leads = (At("leadPort"), At("leadStarboard"));
        stays = [(At("forestayHead"), At("forestayFoot")), (At("backstayHead"), At("backstayFoot"))];
        rest = new SailForm(clews.Port, heads.Port, heads.Starboard, clews.Starboard, camber);
        var step = 2 * Math.PI / Steps;
        var (from, to) = (0.0, 0.0);
        while (from > -Math.PI && Clear(Pose(from - step, 0))) from -= step;
        while (to < Math.PI && Clear(Pose(to + step, 0))) to += step;
        Free = (from, to - from);
    }

    public static SquareRig Of(JsonObject rig, double camber, double clothKilograms, RigWidths widths) =>
        new(rig, camber, (rig["yardKilograms"].AsDouble(), clothKilograms), widths);

    public override HullPoint Belly(RigState state)
    {
        var normal = state.Form.Normal;
        return Math.Sign(normal.Along) == Ahead ? normal : -1 * normal;
    }

    public override int Ahead => -1;

    public override HullPoint Origin => heel;

    public override HullPoint Axis => axis;

    public override Planform Planform => new Rectangle((heads.Port - clews.Port).Length / (heads.Port - heads.Starboard).Length);

    public override RigWidths Widths { get; }

    public override double Resting => Free.From;

    public override int Leeward(Airflow air) => air.Leeward;

    public override double LeeOf(RigState state, (double Reach, double Span) set) => 1;

    private HullPoint Turned(HullPoint point, double yard) => point.Turned(heel, axis, yard);

    public override RigState Pose(double yard, double boom) => Square(yard);

    private SquareState Square(double yard)
    {
        var (clewPort, clewStarboard) = (Turned(clews.Port, yard), Turned(clews.Starboard, yard));
        var form = rest.Posed(clewPort, Turned(heads.Port, yard), Turned(heads.Starboard, yard), clewStarboard);
        if (Set.Span < 1) (clewPort, clewStarboard) = (form.At(0, 0, 0, Set), form.At(1, 0, 0, Set));
        return new SquareState(yard, Turned(arms.Port, yard), Turned(arms.Starboard, yard), clewPort, clewStarboard, form, 0);
    }

    public override RigState Furled => Square(0);

    public override double HoistMetres => 0;

    public override double SpanMetres => (heads.Port - clews.Port).Length;

    public override double Arm(RigState state)
    {
        var offset = ((SquareState)state).ClewPort - heel;
        return (offset - offset.Dot(axis) * axis).Length;
    }

    public override RigState Between(RigState from, RigState to, double share) => to;

    public override RigState Held(double yard, double boom, Func<RigState, (HullPoint At, HullPoint Force)> wind) => Settle(yard, boom, wind);

    public override HullPoint[] Marks(RigState state)
    {
        var square = (SquareState)state;
        return [square.ArmPort, square.ArmStarboard, square.ClewPort, square.ClewStarboard];
    }

    public override RigState Settle(double yard, double near, Func<RigState, (HullPoint At, HullPoint Force)> wind)
    {
        var posed = Square(yard);
        var (at, force) = wind(posed);
        var (clew, lead) = Sheeted(posed, force);
        var sheet = (lead - clew).Unit;
        var reach = (clew - heel).Cross(sheet).Dot(axis);
        var pulled = Math.Abs(reach) > ShortestLeverMetres ? -(at - heel).Cross(force).Dot(axis) / reach : double.NaN;
        return posed with { SheetNewtons = pulled };
    }

    private (HullPoint Clew, HullPoint Lead) Sheeted(SquareState state, HullPoint force) =>
        force.Across < 0 ? (state.ClewPort, leads.Port) : (state.ClewStarboard, leads.Starboard);

    public override bool Clear(RigState state)
    {
        var square = (SquareState)state;
        var (headPort, headStarboard) = (square.Form.At(0, 1, 0), square.Form.At(1, 1, 0));
        return stays.All(stay => !Crosses(stay.From, stay.To, square.ClewPort, headPort, headStarboard)
            && !Crosses(stay.From, stay.To, square.ClewPort, headStarboard, square.ClewStarboard)
            && Apart(square.ArmPort, square.ArmStarboard, stay.From, stay.To) > (Widths.Spar + Widths.Line) / 2);
    }

    public override RigLoad Loads(RigState state, HullPoint at, HullPoint force)
    {
        var square = (SquareState)state;
        var (clew, lead) = Sheeted(square, force);
        var sheet = (lead - clew).Unit;
        var sheeted = square.SheetNewtons > 0 ? square.SheetNewtons : 0;
        var (yard, cloth) = (kilograms.Yard * Gravity, kilograms.Cloth * Gravity);
        var yardMiddle = 0.5 * (square.ArmPort + square.ArmStarboard);
        var held = -1 * (force + yard + cloth + sheeted * sheet);
        var turning = (at - heel).Cross(force + cloth) + (yardMiddle - heel).Cross(yard) + (clew - heel).Cross(sheeted * sheet);
        return new RigLoad([new(heel, -1 * held, turning), new(lead, -sheeted * sheet)], sheeted, 0, [("mast bending", held.Length)]);
    }

    public override string Describe(RigState state)
    {
        var square = (SquareState)state;
        var yard = square.ArmStarboard - square.ArmPort;
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "yard braced {0} about the mast, {1} off her beam; sheets {2:F2} m port, {3:F2} m starboard",
            Degrees(square.Yard), Degrees(Math.Atan2(Math.Abs(yard.Along), Math.Abs(yard.Across))),
            (leads.Port - square.ClewPort).Length, (leads.Starboard - square.ClewStarboard).Length);
    }

    public override void Draw(IRigCanvas canvas, RigState state, double fill)
    {
        var square = (SquareState)state;
        canvas.Cloth(square.Form, fill, Set);
        canvas.Part("yard", arms, (square.ArmPort, square.ArmStarboard));
        var (footPort, footStarboard) = (square.Form.At(0, 0, fill, Set), square.Form.At(1, 0, fill, Set));
        if (Set.Span < 1) canvas.Part("roll", arms, (footPort, footStarboard), Math.Sqrt(1 - Set.Span));
        canvas.Line(footPort, leads.Port, Widths.Line, (leads.Port - footPort).Length);
        canvas.Line(footStarboard, leads.Starboard, Widths.Line, (leads.Starboard - footStarboard).Length);
    }

    public override void DrawFurled(IRigCanvas canvas, RigState state)
    {
        var square = (SquareState)state;
        canvas.Part("yard", arms, (square.ArmPort, square.ArmStarboard));
        canvas.Part("roll", arms, (square.ArmPort, square.ArmStarboard));
    }
}
