using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Wind;

public sealed class Rig
{
    private readonly RigEnd[] ends;

    public Rig(RigEnd[] ends) => this.ends = ends;

    public static Rig? Of(JsonObject rig, double camber) =>
        rig.Exists ? new Rig(rig["ends"].AsArray().Select((end, n) => new RigEnd(RigPose.Of(end["hoisted"]), RigPose.Of(end["lowered"]), n == 0 ? -1 : 1, camber)).ToArray()) : null;

    public int Tacked { get; set; }

    public RigEnd End => ends[Tacked];

    public int Ends => ends.Length;
}

public sealed class RigEnd
{
    private const int Steps = 720;
    private const int Samples = 64;

    public RigEnd(RigPose hoisted, RigPose lowered, int ahead, double camber)
    {
        (Hoisted, Lowered, Ahead) = (hoisted, lowered, ahead);
        Form = new SailForm(hoisted.Tack, hoisted.Tack, hoisted.Peak, hoisted.Clew, hoisted.Tack, hoisted.Masthead - hoisted.Tack, camber);
        Leeward = -Math.Sign(hoisted.ShroudFoot.Across);
        var lee = Enumerable.Range(0, Steps).Select(k => 2 * Math.PI * k / Steps - Math.PI).MaxBy(turn => Leeward * Turned(hoisted.Clew, turn).Across);
        Free = FreeTurns(lee, Blocked().ToArray());
    }

    public int Leeward { get; }

    public RigPose Hoisted { get; }

    public RigPose Lowered { get; }

    public SailForm Form { get; }

    public int Ahead { get; }

    public (double From, double Span) Free { get; }

    public HullPoint Turned(HullPoint point, double turn) => Form.Turned(point, turn);

    public IEnumerable<double> Turns()
    {
        for (var k = 0; k <= Steps; k++) yield return Free.From + Free.Span * k / Steps;
    }

    private (double Along, double Out, double Turn) AboutAxis(HullPoint point)
    {
        var (tack, axis) = (Form.Origin, Form.Axis);
        var offset = point - tack;
        var along = offset.Dot(axis);
        var radial = offset - along * axis;
        var first = (Hoisted.Clew - tack - (Hoisted.Clew - tack).Dot(axis) * axis).Unit;
        return (along, radial.Length, Math.Atan2(radial.Dot(axis.Cross(first)), radial.Dot(first)));
    }

    private IEnumerable<double> Blocked()
    {
        var (peak, clew) = (AboutAxis(Hoisted.Peak), AboutAxis(Hoisted.Clew));
        foreach (var (from, to) in Hoisted.Standing)
        for (var k = 0; k <= Samples; k++)
        {
            var (along, distance, turn) = AboutAxis(from + (double)k / Samples * (to - from));
            if (InClaw(along, distance, (peak.Along, peak.Out), (clew.Along, clew.Out))) yield return turn;
        }
    }

    private static bool InClaw(double along, double distance, (double Along, double Out) peak, (double Along, double Out) clew)
    {
        var det = peak.Along * clew.Out - peak.Out * clew.Along;
        var u = (along * clew.Out - distance * clew.Along) / det;
        var v = (peak.Along * distance - peak.Out * along) / det;
        return u >= 0 && v >= 0 && u + v <= 1;
    }

    private static (double From, double Span) FreeTurns(double lee, double[] blocked)
    {
        if (blocked.Length == 0) return (lee - Math.PI, 2 * Math.PI);

        double Past(double turn) => (turn - lee + 4 * Math.PI) % (2 * Math.PI);
        var after = blocked.Min(Past);
        var before = blocked.Max(Past) - 2 * Math.PI;
        return (lee + before, after - before);
    }
}
