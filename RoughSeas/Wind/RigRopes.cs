using System;
using System.Collections.Generic;
using HarmonyLib;
using RoughSeas.Lines;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Wind;

[ReliesOn("Vintagestory.GameContent.ClothConstraint", "satisfy", "pulls its two points toward rest_length, scaled by inverse_length")]
[ReliesOn("Vintagestory.GameContent.ClothPoint", "update", "holds a pinned point at its entity's position plus pinnedToOffset as is, with NoAttachTransform; weighs a free point GravityStrength · 10 whatever its mass, and damps its velocity as it stands")]
public sealed class RigRopes(ICoreClientAPI capi, Entity vessel) : IDisposable
{
    private const float Resolution = 2;
    private const double SectionMetres = 1 / 16.0;
    private const float VanillaPointsPerMetre = Resolution;
    private const double ShortestSegmentMetres = 0.3;
    private const double LongestSegmentMetres = 1;

    private static readonly AccessTools.FieldRef<ClothSystem, List<ClothConstraint>> Constraints = AccessTools.FieldRefAccess<ClothSystem, List<ClothConstraint>>("Constraints");
    private static readonly AccessTools.FieldRef<ClothConstraint, float> RestLength = AccessTools.FieldRefAccess<ClothConstraint, float>("rest_length");
    private static readonly AccessTools.FieldRef<ClothConstraint, float> InverseLength = AccessTools.FieldRefAccess<ClothConstraint, float>("inverse_length");
    private static readonly AccessTools.FieldRef<ClothConstraint, float> SquaredRestLength = AccessTools.FieldRefAccess<ClothConstraint, float>("squared_rest_length");
    private static readonly AccessTools.FieldRef<ClothPoint, float> GravityStrength = AccessTools.FieldRefAccess<ClothPoint, float>("GravityStrength");

    private readonly ClothManager cloths = capi.ModLoader.GetModSystem<ClothManager>();
    private readonly WindModSystem wind = capi.ModLoader.GetModSystem<WindModSystem>();
    private readonly List<(ClothSystem Rope, HullPoint From, HullPoint To)> ropes = [];
    private readonly Dictionary<int, Vec3d> middles = [];
    private Vec3d? carried;
    private int laid;
    private double sinceLog;

    public void Begin() => laid = 0;

    public void Lay(HullPoint from, HullPoint to, double metres, double width, Func<HullPoint, Vec3d> world)
    {
        if (laid == ropes.Count || metres / Constraints(ropes[laid].Rope).Count is < ShortestSegmentMetres or > LongestSegmentMetres)
        {
            var (start, end) = (world(from), world(to));
            var reach = end.SubCopy(start);
            var spun = Spun(start, start.AddCopy(reach.Normalize().Mul(Math.Max(metres, 1 / Resolution))), width);
            if (laid < ropes.Count)
            {
                var old = ropes[laid].Rope;
                wind.Log(vessel, "rope {0} re-spun: {1:F2} m on {2} segments, it had {3}", laid, metres, Constraints(spun).Count, Constraints(old).Count);
                Retraced(old, spun);
                cloths.UnregisterCloth(old.ClothId);
                ropes[laid] = (spun, from, to);
            }
            else
            {
                Hang(spun, start, end, metres);
                ropes.Add((spun, from, to));
            }
        }
        var rope = ropes[laid].Rope;
        ropes[laid++] = (rope, from, to);
        var constraints = Constraints(rope);
        var each = (float)(metres / constraints.Count);
        foreach (var constraint in constraints)
        {
            RestLength(constraint) = each;
            InverseLength(constraint) = 1 / each;
            SquaredRestLength(constraint) = each * each;
        }
        rope.WalkPoints(point =>
        {
            point.setMass(each * VanillaPointsPerMetre);
            GravityStrength(point) = each * VanillaPointsPerMetre;
        });
    }

    private static void Retraced(ClothSystem from, ClothSystem to)
    {
        var (was, now) = (new List<ClothPoint>(), new List<ClothPoint>());
        from.WalkPoints(was.Add);
        to.WalkPoints(now.Add);
        var along = new double[was.Count];
        for (var k = 1; k < was.Count; k++) along[k] = along[k - 1] + was[k].Pos.DistanceTo(was[k - 1].Pos);
        for (var k = 0; k < now.Count; k++)
        {
            var at = along[^1] * k / Math.Max(1, now.Count - 1);
            var after = Math.Clamp(Array.FindIndex(along, length => length >= at), 1, was.Count - 1);
            var span = along[after] - along[after - 1];
            var t = span > 0 ? (at - along[after - 1]) / span : 0;
            var (a, b) = (was[after - 1], was[after]);
            now[k].Pos.Set(a.Pos.X + t * (b.Pos.X - a.Pos.X), a.Pos.Y + t * (b.Pos.Y - a.Pos.Y), a.Pos.Z + t * (b.Pos.Z - a.Pos.Z));
            now[k].Velocity.Set(a.Velocity.X + (float)t * (b.Velocity.X - a.Velocity.X), a.Velocity.Y + (float)t * (b.Velocity.Y - a.Velocity.Y),
                a.Velocity.Z + (float)t * (b.Velocity.Z - a.Velocity.Z));
        }
    }

    public void End()
    {
        for (var k = laid; k < ropes.Count; k++) cloths.UnregisterCloth(ropes[k].Rope.ClothId);
        ropes.RemoveRange(laid, ropes.Count - laid);
    }

    public void Pin(Func<HullPoint, Vec3d> world)
    {
        var at = vessel.Pos;
        var hull = world(default);
        var moved = carried is { } was ? hull.SubCopy(was) : new Vec3d();
        carried = hull;
        foreach (var (rope, from, to) in ropes)
        {
            rope.WalkPoints(point => point.Pos.Add(moved));
            Offset(rope.FirstPoint, world(from), at);
            Offset(rope.LastPoint, world(to), at);
        }
    }

    public void Watch(double seconds)
    {
        sinceLog += seconds;
        var periodic = sinceLog >= wind.Config.LogSeconds;
        if (periodic) sinceLog = 0;
        for (var k = 0; k < ropes.Count; k++)
        {
            var rope = ropes[k].Rope;
            var points = new List<ClothPoint>();
            rope.WalkPoints(points.Add);
            var (first, last, middle) = (rope.FirstPoint.Pos, rope.LastPoint.Pos, points[points.Count / 2].Pos);
            var off = middle.SubCopy((first.X + last.X) / 2, (first.Y + last.Y) / 2, (first.Z + last.Z) / 2);
            var rest = Constraints(rope).Count * RestLength(Constraints(rope)[0]);
            var speed = middles.TryGetValue(k, out var was) && seconds > 0 ? off.DistanceTo(was) / seconds : 0;
            middles[k] = off;
            if (periodic || speed > wind.Config.JumpMetresPerSecond)
            {
                wind.Log(vessel, "rope {0} ({1:F2} m on a {2:F2} m chord, {3} segments): its middle {4:F2} m below the chord, {5:F2} m off it, moving {6:F1} m/s against its ends{7}",
                    k, rest, first.DistanceTo(last), Constraints(rope).Count, -off.Y, Math.Sqrt(off.X * off.X + off.Z * off.Z), speed, periodic ? "" : ": jumped");
            }
        }
    }

    public void Dispose()
    {
        foreach (var (rope, _, _) in ropes) cloths.UnregisterCloth(rope.ClothId);
        ropes.Clear();
    }

    private ClothSystem Spun(Vec3d from, Vec3d to, double width)
    {
        var rope = ClothSystem.CreateRope(capi, cloths, from, to, null, Resolution);
        var thickness = (float)(width / SectionMetres);
        (rope.Active, rope.CanPull, rope.CanRip, rope.RopeRenderThickness) = (true, false, false, thickness);
        foreach (var end in rope.Ends)
        {
            end.PinTo(vessel, new Vec3f());
            end.NoAttachTransform = true;
        }
        VanillaRopes.Own(cloths, rope);
        return rope;
    }

    private static void Hang(ClothSystem rope, Vec3d from, Vec3d to, double metres)
    {
        var chord = from.DistanceTo(to);
        var sag = Math.Sqrt(3 * chord * Math.Max(0, metres - chord) / 8);
        var points = new List<ClothPoint>();
        rope.WalkPoints(points.Add);
        for (var k = 0; k < points.Count; k++)
        {
            var t = (double)k / Math.Max(1, points.Count - 1);
            points[k].Pos.Set(from.X + t * (to.X - from.X), from.Y + t * (to.Y - from.Y) - 4 * sag * t * (1 - t), from.Z + t * (to.Z - from.Z));
        }
    }

    private static void Offset(ClothPoint end, Vec3d world, EntityPos at)
    {
        end.pinnedToOffset.Set((float)(world.X - at.X), (float)(world.Y - at.Y), (float)(world.Z - at.Z));
        end.Pos.Set(world);
    }
}
