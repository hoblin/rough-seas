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
[ReliesOn("Vintagestory.GameContent.ClothPoint", "update", "holds a pinned point at its entity's position plus pinnedToOffset as is, with NoAttachTransform")]
public sealed class RigRopes(ICoreClientAPI capi, Entity vessel) : IDisposable
{
    private const float Resolution = 2;
    private const double SectionMetres = 1 / 16.0;

    private static readonly AccessTools.FieldRef<ClothSystem, List<ClothConstraint>> Constraints = AccessTools.FieldRefAccess<ClothSystem, List<ClothConstraint>>("Constraints");
    private static readonly AccessTools.FieldRef<ClothConstraint, float> RestLength = AccessTools.FieldRefAccess<ClothConstraint, float>("rest_length");
    private static readonly AccessTools.FieldRef<ClothConstraint, float> InverseLength = AccessTools.FieldRefAccess<ClothConstraint, float>("inverse_length");
    private static readonly AccessTools.FieldRef<ClothConstraint, float> SquaredRestLength = AccessTools.FieldRefAccess<ClothConstraint, float>("squared_rest_length");

    private readonly ClothManager cloths = capi.ModLoader.GetModSystem<ClothManager>();
    private readonly List<(ClothSystem Rope, HullPoint From, HullPoint To)> ropes = [];
    private int laid;

    public void Begin() => laid = 0;

    public void Lay(HullPoint from, HullPoint to, double metres, double width, Func<HullPoint, Vec3d> world)
    {
        var segments = Math.Max(1, (int)(metres * Resolution));
        if (laid == ropes.Count || Constraints(ropes[laid].Rope) is var had && (had.Count < segments || had.Count > 2 * segments + 1))
        {
            if (laid < ropes.Count) cloths.UnregisterCloth(ropes[laid].Rope.ClothId);
            var (start, end) = (world(from), world(to));
            var reach = end.SubCopy(start);
            var spun = Spun(start, start.AddCopy(reach.Normalize().Mul(Math.Max(metres, 1 / Resolution))), width);
            Hang(spun, start, end, metres);
            if (laid == ropes.Count) ropes.Add((spun, from, to));
            else ropes[laid] = (spun, from, to);
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
    }

    public void End()
    {
        for (var k = laid; k < ropes.Count; k++) cloths.UnregisterCloth(ropes[k].Rope.ClothId);
        ropes.RemoveRange(laid, ropes.Count - laid);
    }

    public void Pin(Func<HullPoint, Vec3d> world)
    {
        var at = vessel.Pos;
        foreach (var (rope, from, to) in ropes)
        {
            Offset(rope.FirstPoint, world(from), at);
            Offset(rope.LastPoint, world(to), at);
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
        rope.WalkPoints(point => point.setMass(point.Mass * thickness * thickness));
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
