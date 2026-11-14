using System;
using System.Collections.Generic;

namespace RoughSeas.Wind;

public record RigState(double Yard, double Boom, SailForm Form, double SheetNewtons)
{
    public HullPoint Centre((double Reach, double Span) set, double depth) => Form.Centre(set, depth);
}

public sealed record RigWidths(double Spar, double Pole, double Shore, double Line, double Roll);

public sealed record RigLoad(Push[] Points, double SheetNewtons, double HalyardNewtons, (string Line, double Newtons)[] Stays);

public interface IRigCanvas
{
    void Spar(HullPoint from, HullPoint to, double width);

    void Line(HullPoint from, HullPoint to, double width);

    void Roll(HullPoint from, HullPoint to, double width);

    void Cloth(SailForm form, double fill, (double Reach, double Span) set);
}

/// <summary>
/// A set sail and what holds it, in her hull's frame: the spars turn about <see cref="Axis"/> through <see cref="Origin"/>
/// only as far as no spar or cloth meets the mast or a stay, and the loads it hands her hull sum to the sail's force and
/// the weight of its spars and cloth. Physics and drawing pose it the same way.
/// </summary>
public abstract class Rigging
{
    protected const int Steps = 720;

    public abstract int Ahead { get; }

    public abstract HullPoint Origin { get; }

    public abstract HullPoint Axis { get; }

    public abstract RigWidths Widths { get; }

    public (double From, double Span) Free { get; protected set; }

    public abstract double Resting { get; }

    public abstract HullPoint Belly(RigState state);

    public virtual bool Aback(RigState state, Airflow air)
    {
        var belly = Belly(state);
        return belly.Along * air.Along + belly.Across * air.Across < 0;
    }

    public abstract int Leeward(Airflow air);

    public abstract double LeeOf(RigState state, (double Reach, double Span) set);

    public abstract RigState Pose(double yard, double boom);

    public abstract RigState Settle(double yard, double near, Func<RigState, (HullPoint At, HullPoint Force)> wind);

    public virtual RigState Track(double yard, double near, Func<RigState, (HullPoint At, HullPoint Force)> wind) => Settle(yard, near, wind);

    public abstract bool Clear(RigState state);

    public abstract RigLoad Loads(RigState state, HullPoint at, HullPoint force);

    public abstract string Describe(RigState state);

    public abstract void Draw(IRigCanvas canvas, RigState state, double fill, (double Reach, double Span) set);

    public abstract void DrawFurled(IRigCanvas canvas);

    protected static bool Crosses(HullPoint from, HullPoint to, HullPoint a, HullPoint b, HullPoint c)
    {
        var (edge1, edge2, direction) = (b - a, c - a, to - from);
        var p = direction.Cross(edge2);
        var det = edge1.Dot(p);
        if (Math.Abs(det) < 1e-12) return false;

        var s = from - a;
        var u = s.Dot(p) / det;
        var q = s.Cross(edge1);
        var v = direction.Dot(q) / det;
        var t = edge2.Dot(q) / det;
        return u >= 0 && v >= 0 && u + v <= 1 && t > 0 && t < 1;
    }

    protected static double Apart(HullPoint a0, HullPoint a1, HullPoint b0, HullPoint b1)
    {
        var (u, v, w) = (a1 - a0, b1 - b0, a0 - b0);
        var (uu, uv, vv, uw, vw) = (u.Dot(u), u.Dot(v), v.Dot(v), u.Dot(w), v.Dot(w));
        var det = uu * vv - uv * uv;
        var s = det > 1e-12 ? Math.Clamp((uv * vw - vv * uw) / det, 0, 1) : 0;
        var t = Math.Clamp((uv * s + vw) / vv, 0, 1);
        s = Math.Clamp((uv * t - uw) / uu, 0, 1);
        return (w + s * u - t * v).Length;
    }

    protected static string Degrees(double radians) => $"{radians * 180 / Math.PI:F0}°";
}
