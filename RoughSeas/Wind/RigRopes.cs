using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.Lines;
using RoughSeas.Water;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Wind;

public sealed class RigRopes(ICoreClientAPI capi, Entity vessel, Fibre fibre) : IDisposable
{
    private const float Resolution = 2;
    private const double SectionMetres = 1 / 16.0;
    private const double ShortestSegmentMetres = 0.3;
    private const double LongestSegmentMetres = 1;
    private const double LongestStepSeconds = 0.1;
    private const double LowestMetres = 0.1;

    private sealed class Rope(ClothSystem drawn, List<ClothPoint> points, Chain chain)
    {
        public ClothSystem Drawn => drawn;
        public List<ClothPoint> Points => points;
        public Chain Chain => chain;
        public HullPoint From { get; set; }
        public HullPoint To { get; set; }
        public Vec3d? Middle { get; set; }
    }

    private readonly ClothManager cloths = capi.ModLoader.GetModSystem<ClothManager>();
    private readonly WindModSystem wind = capi.ModLoader.GetModSystem<WindModSystem>();
    private readonly List<Rope> ropes = [];
    private int laid;
    private double sinceLog;

    public void Begin() => laid = 0;

    public void Lay(HullPoint from, HullPoint to, double metres, double width, Func<HullPoint, Vec3d> world)
    {
        if (laid == ropes.Count || metres / (ropes[laid].Points.Count - 1) is < ShortestSegmentMetres or > LongestSegmentMetres)
        {
            var (start, end) = (world(from), world(to));
            var drawn = Spun(start, start.AddCopy(end.SubCopy(start).Normalize().Mul(Math.Max(metres, 1 / Resolution))), width);
            var points = new List<ClothPoint>();
            drawn.WalkPoints(points.Add);
            var placed = laid < ropes.Count ? Retraced(ropes[laid].Chain, points.Count) : Hung(start, end, metres, points.Count);
            var rope = new Rope(drawn, points, new Chain(placed.Points, metres, width, fibre, wind.Config.AirKgPerCubicMetre));
            Array.Copy(placed.Moving, rope.Chain.Moving, placed.Moving.Length);
            if (laid < ropes.Count)
            {
                wind.Log(vessel, "rope {0} re-spun: {1:F2} m on {2} segments, it had {3}", laid, metres, points.Count - 1, ropes[laid].Points.Count - 1);
                cloths.UnregisterCloth(ropes[laid].Drawn.ClothId);
                ropes[laid] = rope;
            }
            else ropes.Add(rope);
        }
        var laidRope = ropes[laid++];
        (laidRope.From, laidRope.To) = (from, to);
        laidRope.Chain.Rest(metres);
    }

    public void End()
    {
        for (var k = laid; k < ropes.Count; k++) cloths.UnregisterCloth(ropes[k].Drawn.ClothId);
        ropes.RemoveRange(laid, ropes.Count - laid);
    }

    public void Pull(Func<HullPoint, Vec3d> world, (double X, double Z) blowing, double seconds)
    {
        var step = Math.Min(seconds, LongestStepSeconds);
        foreach (var rope in ropes)
        {
            var (from, to) = (world(rope.From), world(rope.To));
            var moved = from.SubCopy(rope.Chain.Points[0]);
            if (step > 0 && moved.Length() / step > wind.Config.JumpMetresPerSecond) rope.Chain.Carry(moved);
            var height = Math.Max(LowestMetres, (from.Y + to.Y) / 2 - vessel.Pos.Y) / wind.Config.ReferenceHeightMetres;
            var share = Math.Pow(height, wind.Config.GradientExponent);
            rope.Chain.Step(from, to, WaveTheory.Gravity, new Vec3d(blowing.X * share, 0, blowing.Z * share), step);
            for (var k = 0; k < rope.Points.Count; k++) rope.Points[k].Pos.Set(rope.Chain.Points[k]);
        }
        Watch(seconds);
    }

    private void Watch(double seconds)
    {
        sinceLog += seconds;
        var periodic = sinceLog >= wind.Config.LogSeconds;
        if (periodic) sinceLog = 0;
        for (var k = 0; k < ropes.Count; k++)
        {
            var (rope, points) = (ropes[k], ropes[k].Chain.Points);
            var (first, last, middle) = (points[0], points[^1], points[points.Length / 2]);
            var off = middle.SubCopy((first.X + last.X) / 2, (first.Y + last.Y) / 2, (first.Z + last.Z) / 2);
            var speed = rope.Middle is { } was && seconds > 0 ? off.DistanceTo(was) / seconds : 0;
            rope.Middle = off;
            if (periodic || speed > wind.Config.JumpMetresPerSecond)
            {
                wind.Log(vessel, "rope {0} ({1:F2} m on a {2:F2} m chord, {3} segments): its middle {4:F2} m below the chord, {5:F2} m off it, moving {6:F1} m/s against its ends{7}",
                    k, rope.Chain.RestMetres, first.DistanceTo(last), points.Length - 1, -off.Y, Math.Sqrt(off.X * off.X + off.Z * off.Z), speed, periodic ? "" : ": jumped");
            }
        }
    }

    public void Dispose()
    {
        foreach (var rope in ropes) cloths.UnregisterCloth(rope.Drawn.ClothId);
        ropes.Clear();
    }

    private ClothSystem Spun(Vec3d from, Vec3d to, double width)
    {
        var rope = ClothSystem.CreateRope(capi, cloths, from, to, null, Resolution);
        (rope.Active, rope.CanPull, rope.CanRip, rope.RopeRenderThickness) = (true, false, false, (float)(width / SectionMetres));
        VanillaRopes.Own(cloths, rope);
        return rope;
    }

    private static (Vec3d[] Points, Vec3d[] Moving) Hung(Vec3d from, Vec3d to, double metres, int count)
    {
        var sag = Math.Sqrt(3 * from.DistanceTo(to) * Math.Max(0, metres - from.DistanceTo(to)) / 8);
        var points = Enumerable.Range(0, count).Select(k => (double)k / Math.Max(1, count - 1))
            .Select(t => new Vec3d(from.X + t * (to.X - from.X), from.Y + t * (to.Y - from.Y) - 4 * sag * t * (1 - t), from.Z + t * (to.Z - from.Z))).ToArray();
        return (points, Array.ConvertAll(points, _ => new Vec3d()));
    }

    private static (Vec3d[] Points, Vec3d[] Moving) Retraced(Chain chain, int count)
    {
        var (was, moving) = (chain.Points, chain.Moving);
        var along = new double[was.Length];
        for (var k = 1; k < was.Length; k++) along[k] = along[k - 1] + was[k].DistanceTo(was[k - 1]);
        var (points, speeds) = (new Vec3d[count], new Vec3d[count]);
        for (var k = 0; k < count; k++)
        {
            var at = along[^1] * k / Math.Max(1, count - 1);
            var after = Math.Clamp(Array.FindIndex(along, length => length >= at), 1, was.Length - 1);
            var span = along[after] - along[after - 1];
            var t = span > 0 ? (at - along[after - 1]) / span : 0;
            points[k] = Between(was[after - 1], was[after], t);
            speeds[k] = Between(moving[after - 1], moving[after], t);
        }
        return (points, speeds);
    }

    private static Vec3d Between(Vec3d a, Vec3d b, double t) => new(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y), a.Z + t * (b.Z - a.Z));
}
