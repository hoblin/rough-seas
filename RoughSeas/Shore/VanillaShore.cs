using System;
using System.Collections.Generic;
using HarmonyLib;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace RoughSeas.Shore;

public static class VanillaShore
{
    private const string HarmonyId = "roughseas.shore.vanilla";

    private static Harmony? harmony;

    public static void Attach()
    {
        if (harmony != null) return;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(BlockWater), nameof(BlockWater.ShouldReceiveServerGameTicks)),
            prefix: new HarmonyMethod(typeof(VanillaShore), nameof(TickSolid)));
        harmony.Patch(AccessTools.Method(typeof(BlockWater), nameof(BlockWater.OnServerGameTick)),
            prefix: new HarmonyMethod(typeof(VanillaShore), nameof(TickedSolid)));
        harmony.Patch(AccessTools.Method(typeof(GameMain), nameof(GameMain.GetBlockIntersectionBoxes)),
            prefix: new HarmonyMethod(typeof(VanillaShore), nameof(SelectLand)));
        harmony.Patch(AccessTools.Method(typeof(BlockLiquidContainerBase), nameof(BlockLiquidContainerBase.TryFillFromBlock)),
            prefix: new HarmonyMethod(typeof(VanillaShore), nameof(FillFromWater)));
        foreach (var accessor in new[] { typeof(BlockAccessorBase), typeof(BlockAccessorReadLockfree) })
        {
            harmony.Patch(AccessTools.Method(accessor, nameof(IBlockAccessor.SearchFluidBlocks)),
                prefix: new HarmonyMethod(typeof(VanillaShore), nameof(SearchOpenWater)));
        }
        harmony.Patch(AccessTools.Method(typeof(EntityBobber), "getPondSize"),
            transpiler: new HarmonyMethod(typeof(VanillaShore), nameof(CountOpenWater)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
    }

    private static bool TickSolid(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object? extra, ref bool __result)
    {
        extra = null;
        if (!OpenWater.Land(world.BlockAccessor, pos)) return true;
        var solid = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.Solid);
        __result = solid.ShouldReceiveServerGameTicks(world, pos, offThreadRandom, out var solidExtra);
        extra = new SolidTick(solid, solidExtra);
        return false;
    }

    private static bool TickedSolid(IWorldAccessor world, BlockPos pos, object? extra)
    {
        if (extra is not SolidTick tick) return true;
        tick.Block.OnServerGameTick(world, pos, tick.Extra);
        return false;
    }

    private static void SelectLand(GameMain __instance, BlockPos pos, ref bool liquidSelectable)
    {
        if (liquidSelectable && OpenWater.Land(__instance.blockAccessor, pos)) liquidSelectable = false;
    }

    private static bool FillFromWater(EntityAgent byEntity, BlockPos pos, ref bool __result)
    {
        if (!OpenWater.Land(byEntity.World.BlockAccessor, pos)) return true;
        __result = false;
        return false;
    }

    private static void SearchOpenWater(IBlockAccessor __instance, ref ActionConsumable<Block, BlockPos> onBlock)
    {
        var (found, blocks) = (onBlock, __instance);
        onBlock = (fluid, pos) => found(fluid.Id != 0 && OpenWater.Land(blocks, pos) ? blocks.GetBlock(0) : fluid, pos);
    }

    private static IEnumerable<CodeInstruction> CountOpenWater(IEnumerable<CodeInstruction> instructions) =>
        new CodeMatcher(instructions)
            .MatchStartForward(new CodeMatch(code => code.Calls(AccessTools.Method(typeof(IBlockAccessor), nameof(IBlockAccessor.GetBlock), [typeof(BlockPos), typeof(int)]))))
            .ThrowIfInvalid("EntityBobber.getPondSize no longer reads the fluid layer through IBlockAccessor.GetBlock")
            .SetInstruction(CodeInstruction.Call(typeof(OpenWater), nameof(OpenWater.Layer)))
            .InstructionEnumeration();

    private sealed record SolidTick(Block Block, object? Extra);
}
