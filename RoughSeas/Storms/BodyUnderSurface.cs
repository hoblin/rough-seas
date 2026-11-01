using System;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

/// <summary>
/// Puts a player's body in the water by the drawn sea, not by the water blocks: in it under a crest that rises over
/// the still water, out of it in a trough that sinks below. Vanilla decides both from the blocks:
/// <c>EntityBehaviorControlledPhysics.ApplyTests</c> sets <c>Swimming</c> and <c>FeetInLiquid</c>, which gravity, drag
/// and the swim stroke go by, and <c>PModulePlayerInLiquid.HandleSwimming</c> lifts a stroke by how deep the body lies
/// under the blocks' water. On a client alone, where the sea is drawn and a player's own movement is simulated; the
/// other players' bodies there follow the same drawn sea, and calm water is taken where vanilla draws it. Where
/// <see cref="DrawnWater.WaterAt(IBlockAccessor, double, double, double)"/> finds no water under the body, both stay
/// vanilla's. What holds the body up is <see cref="BodyAfloat"/>'s.
/// </summary>
public static class BodyUnderSurface
{
    private const string HarmonyId = "roughseas.storms.body";

    private static readonly Dictionary<long, (bool Drawn, bool Swimming, bool FeetIn)> decided = [];
    private static Harmony? harmony;
    private static DrawnWater? water;
    private static SwimConfig config = new();
    private static StormsModSystem? system;

    public static void Attach(StormsModSystem storms, DrawnWater drawn, SwimConfig swim)
    {
        if (harmony != null) return;
        (system, water, config) = (storms, drawn, swim);
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(EntityBehaviorControlledPhysics), nameof(EntityBehaviorControlledPhysics.ApplyTests)),
            postfix: new HarmonyMethod(typeof(BodyUnderSurface), nameof(InWater)) { priority = Priority.High });
        harmony.Patch(AccessTools.Method(typeof(PModulePlayerInLiquid), nameof(PModulePlayerInLiquid.HandleSwimming)),
            prefix: new HarmonyMethod(typeof(BodyUnderSurface), nameof(BeforeStroke)),
            postfix: new HarmonyMethod(typeof(BodyUnderSurface), nameof(Stroke)) { priority = Priority.High });
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
        (system, water) = (null, null);
        decided.Clear();
    }

    // runs after vanilla has set the flags from the blocks, and before the next tick's gravity, drag and stroke read them
    private static void InWater(EntityBehaviorControlledPhysics __instance, EntityPos pos)
    {
        if (__instance.entity is not EntityPlayer { World.Side: EnumAppSide.Client } player) return;
        var body = BodyAt(player, pos);
        Decided(player, pos, body);
        if (body is not { } immersion) return;
        player.Swimming = immersion.Swimming;
        player.FeetInLiquid = immersion.FeetIn;
    }

    private static void BeforeStroke(EntityPos pos, out double __state) => __state = pos.Motion.Y;

    // vanilla lifted the stroke by the body's depth under the blocks' water; lift it by its depth under the drawn surface instead
    private static void Stroke(PModulePlayerInLiquid __instance, float dt, Entity entity, EntityPos pos, EntityControls controls, double __state)
    {
        if (entity.World.Side != EnumAppSide.Client || BodyAt(entity, pos) is not { } body) return;
        var stroke = new Stroke(controls.Jump, controls.TriesToMove, controls.FlyVector.Y, __instance.Push);
        pos.Motion.Y = __state + Immersion.Lift(config, body.Share(config), stroke, dt);
    }

    private static Immersion? BodyAt(Entity entity, EntityPos pos) =>
        water?.WaterAt(entity.World.BlockAccessor, pos.X, pos.Y, pos.Z) is { } surface
            ? new Immersion(surface.Height, pos.Y, pos.Y + entity.SwimmingOffsetY)
            : null;

    private static void Decided(EntityPlayer player, EntityPos pos, Immersion? body)
    {
        var now = body is { } drawn ? (true, drawn.Swimming, drawn.FeetIn) : (false, player.Swimming, player.FeetInLiquid);
        if (decided.TryGetValue(player.EntityId, out var last) && last == now) return;
        decided[player.EntityId] = now;
        if (body is not { } immersion)
        {
            system?.Log(player, "body at y {0:0.00}: no drawn surface over it, the blocks decide (swimming {1}, feet in water {2})",
                pos.Y, player.Swimming, player.FeetInLiquid);
            return;
        }
        system?.Log(player, "body at y {0:0.00}: chest {1} the drawn surface by {2:0.00} m, swimming {3}, feet in water {4} (the blocks say {5}, {6})",
            pos.Y, immersion.Depth > 0 ? "under" : "above", Math.Abs(immersion.Depth), immersion.Swimming, immersion.FeetIn,
            player.Swimming, player.FeetInLiquid);
    }
}
