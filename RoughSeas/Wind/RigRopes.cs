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
    private const float Diameter = 0.5f;

    private static readonly AccessTools.FieldRef<ClothSystem, List<ClothConstraint>> Constraints = AccessTools.FieldRefAccess<ClothSystem, List<ClothConstraint>>("Constraints");
    private static readonly AccessTools.FieldRef<ClothConstraint, float> RestLength = AccessTools.FieldRefAccess<ClothConstraint, float>("rest_length");
    private static readonly AccessTools.FieldRef<ClothConstraint, float> InverseLength = AccessTools.FieldRefAccess<ClothConstraint, float>("inverse_length");
    private static readonly AccessTools.FieldRef<ClothConstraint, float> SquaredRestLength = AccessTools.FieldRefAccess<ClothConstraint, float>("squared_rest_length");

    private readonly ClothManager cloths = capi.ModLoader.GetModSystem<ClothManager>();
    private readonly List<(ClothSystem Rope, HullPoint From, HullPoint To)> ropes = [];
    private int laid;

    public void Begin() => laid = 0;

    public void Lay(HullPoint from, HullPoint to, double metres, Func<HullPoint, Vec3d> world)
    {
        if (laid == ropes.Count) ropes.Add((Spun(world(from), world(to)), from, to));
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
        for (var k = laid; k < ropes.Count; k++) VanillaRopes.Drop(cloths, ropes[k].Rope);
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
        foreach (var (rope, _, _) in ropes) VanillaRopes.Drop(cloths, rope);
        ropes.Clear();
    }

    private ClothSystem Spun(Vec3d from, Vec3d to)
    {
        var rope = ClothSystem.CreateRope(capi, cloths, from, to, null);
        (rope.Active, rope.CanPull, rope.CanRip, rope.RopeRenderThickness) = (true, false, false, Diameter);
        rope.WalkPoints(point => point.setMass(point.Mass * Diameter * Diameter));
        foreach (var end in rope.Ends)
        {
            end.PinTo(vessel, new Vec3f());
            end.NoAttachTransform = true;
        }
        VanillaRopes.Own(cloths, rope);
        return rope;
    }

    private static void Offset(ClothPoint end, Vec3d world, EntityPos at) =>
        end.pinnedToOffset.Set((float)(world.X - at.X), (float)(world.Y - at.Y), (float)(world.Z - at.Z));
}
