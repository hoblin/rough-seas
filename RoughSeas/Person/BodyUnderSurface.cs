using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RoughSeas.Storms;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Person;

/// <summary>
/// Puts a player's body in the water by the drawn sea, not by the water blocks: in it under a crest that rises over the
/// still water, out of it in a trough that sinks below. Vanilla decides both from the blocks:
/// <c>EntityBehaviorControlledPhysics.ApplyTests</c> sets <c>Swimming</c> and <c>FeetInLiquid</c>, which gravity, drag and
/// the swim stroke go by, and <c>PModulePlayerInLiquid.HandleSwimming</c> lifts a stroke by how deep the chest lies under
/// the blocks' water. On both sides: a client through the sea it draws, the server through the same sea built around the
/// body (<see cref="RegionSwells"/>), so wetness, torches and breath there read what the client floats on. Where
/// <see cref="DrawnWater.WaterAt(IBlockAccessor, double, double, double)"/> finds no water under the body, in another
/// dimension or on a seat, both stay vanilla's. What holds the body up is <see cref="BodyAfloat"/>'s.
/// </summary>
public static class BodyUnderSurface
{
    private const string HarmonyId = "roughseas.person.body";
    private const int WaterAroundBlocks = 2;

    private static readonly ConcurrentDictionary<(long, EnumAppSide), (bool Drawn, bool Swimming, bool FeetIn)> decided = [];
    private static Harmony? harmony;
    private static DrawnWater? clientWater;
    private static RegionSwells? serverSwells;
    private static PersonModSystem? system;

    public static void AttachClient(PersonModSystem mod, DrawnWater? drawn)
    {
        clientWater = drawn;
        Patch(mod);
    }

    public static void AttachServer(PersonModSystem mod, RegionSwells? swells)
    {
        serverSwells = swells;
        Patch(mod);
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
        (system, clientWater, serverSwells) = (null, null, null);
        decided.Clear();
    }

    private static void Patch(PersonModSystem mod)
    {
        system = mod;
        if (harmony != null) return;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(EntityBehaviorControlledPhysics), nameof(EntityBehaviorControlledPhysics.ApplyTests)),
            postfix: new HarmonyMethod(typeof(BodyUnderSurface), nameof(InWater)) { priority = Priority.High });
        harmony.Patch(AccessTools.Method(typeof(PModulePlayerInLiquid), nameof(PModulePlayerInLiquid.HandleSwimming)),
            transpiler: new HarmonyMethod(typeof(BodyUnderSurface), nameof(DrawnStroke)));
    }

    // runs after vanilla has set the flags from the blocks, and before the next tick's gravity, drag and stroke read them
    private static void InWater(EntityBehaviorControlledPhysics __instance, EntityPos pos)
    {
        if (__instance.entity is not EntityPlayer player) return;
        var body = BodyAt(player, pos);
        Decided(player, pos, body);
        if (body is not { } immersion) return;
        player.Swimming = immersion.Swimming;
        player.FeetInLiquid = immersion.FeetIn;
    }

    /// <summary>
    /// Vanilla's stroke stays vanilla's, its numbers and any other mod's patch to it, and only the depth it scales the
    /// stroke's lift by is read under the drawn surface: its one call of <c>GameMath.Clamp</c> takes that depth.
    /// </summary>
    private static IEnumerable<CodeInstruction> DrawnStroke(IEnumerable<CodeInstruction> code)
    {
        var clamp = AccessTools.Method(typeof(GameMath), nameof(GameMath.Clamp), [typeof(float), typeof(float), typeof(float)]);
        var found = false;
        foreach (var instruction in code)
        {
            if (!found && instruction.Calls(clamp))
            {
                found = true;
                yield return new CodeInstruction(OpCodes.Ldarg_2);
                yield return new CodeInstruction(OpCodes.Ldarg_3);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BodyUnderSurface), nameof(DrawnClamp)));
                continue;
            }
            yield return instruction;
        }
        if (!found) system?.Log("vanilla's stroke has changed: it keeps the blocks' depth");
    }

    private static float DrawnClamp(float depth, float least, float most, Entity entity, EntityPos pos) =>
        GameMath.Clamp(BodyAt(entity, pos) is { } body ? (float)body.Depth : depth, least, most);

    private static Immersion? BodyAt(Entity entity, EntityPos pos)
    {
        if (pos.Dimension != Dimensions.NormalWorld || entity is EntityAgent { MountedOn: not null } ||
            !DrawnWater.Near(entity.World.BlockAccessor, pos.X, pos.Y, pos.Z, WaterAroundBlocks)) return null;
        var water = entity.World.Side == EnumAppSide.Client ? clientWater : serverSwells?.Around(pos.X, pos.Z).Water;
        return water?.WaterAt(entity.World.BlockAccessor, pos.X, pos.Y, pos.Z) is { } surface
            ? new Immersion(surface.Height, pos.Y, pos.Y + entity.SwimmingOffsetY)
            : null;
    }

    private static void Decided(EntityPlayer player, EntityPos pos, Immersion? body)
    {
        var now = body is { } drawn ? (true, drawn.Swimming, drawn.FeetIn) : (false, player.Swimming, player.FeetInLiquid);
        var key = (player.EntityId, player.World.Side);
        if (decided.TryGetValue(key, out var last) && last == now) return;
        decided[key] = now;
        if (body is not { } immersion)
        {
            system?.Log(player, "body at y {0:0.00}: no drawn surface over it, the blocks decide on the {1} (swimming {2}, feet in water {3})",
                pos.Y, player.World.Side, player.Swimming, player.FeetInLiquid);
            return;
        }
        system?.Log(player, "body at y {0:0.00}: chest {1} the drawn surface by {2:0.00} m, swimming {3}, feet in water {4} on the {5} (the blocks say {6}, {7})",
            pos.Y, immersion.Depth > 0 ? "under" : "above", Math.Abs(immersion.Depth), immersion.Swimming, immersion.FeetIn, player.World.Side,
            player.Swimming, player.FeetInLiquid);
    }
}
