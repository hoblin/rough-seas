using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Common;
using Vintagestory.Essentials;
using Vintagestory.GameContent;

namespace RoughSeas.Shore;

/// <summary>
/// Keeps vanilla's readers of the fluid layer on land over the water under the shore: a random tick at such a position
/// goes to its solid block, which the fluid would otherwise take (and freeze); a liquid container neither picks nor fills
/// from it; farmland and anything else searching for water around it, a bobber counting its pond, a creature finding its
/// path or picking where to wander don't count it.
/// </summary>
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
        harmony.Patch(AccessTools.Method(typeof(GameMain), nameof(GameMain.GetBlockIntersectionBoxes), [typeof(BlockPos), typeof(bool)]),
            prefix: new HarmonyMethod(typeof(VanillaShore), nameof(SelectLand)));
        harmony.Patch(AccessTools.Method(typeof(BlockLiquidContainerBase), nameof(BlockLiquidContainerBase.TryFillFromBlock), [typeof(ItemSlot), typeof(EntityAgent), typeof(BlockPos)]),
            prefix: new HarmonyMethod(typeof(VanillaShore), nameof(FillFromWater)));
        harmony.Patch(AccessTools.Method(typeof(BlockAccessorBase), nameof(IBlockAccessor.SearchFluidBlocks)),
            prefix: new HarmonyMethod(typeof(VanillaShore), nameof(SearchOpenWater)));
        foreach (var reader in new[]
        {
            AccessTools.Method(typeof(EntityBobber), "getPondSize"),
            AccessTools.Method(typeof(AStar), "traversable"),
            AccessTools.Method(typeof(AiTaskWander), nameof(AiTaskWander.loadNextWanderTarget)),
        })
        {
            harmony.Patch(reader, transpiler: new HarmonyMethod(typeof(VanillaShore), nameof(ReadOpenWater)));
        }
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
        __result = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.Solid).ShouldReceiveServerGameTicks(world, pos, offThreadRandom, out var solidExtra);
        // A tick with an extra goes to the block in the default layer, which is the solid one; without one, to the fluid.
        extra = solidExtra ?? SolidTick;
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

    private static IEnumerable<CodeInstruction> ReadOpenWater(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var reads = new Dictionary<MethodInfo, MethodInfo>
        {
            [AccessTools.Method(typeof(IBlockAccessor), nameof(IBlockAccessor.GetBlock), [typeof(BlockPos), typeof(int)])] =
                AccessTools.Method(typeof(OpenWater), nameof(OpenWater.Layer)),
            [AccessTools.Method(typeof(IBlockAccessor), nameof(IBlockAccessor.GetBlockRaw), [typeof(int), typeof(int), typeof(int), typeof(int)])] =
                AccessTools.Method(typeof(OpenWater), nameof(OpenWater.LayerRaw)),
        };
        var replaced = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.operand is MethodInfo called && reads.TryGetValue(called, out var open))
            {
                replaced++;
                yield return new CodeInstruction(OpCodes.Call, open).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                continue;
            }
            yield return instruction;
        }
        if (replaced == 0) throw new InvalidOperationException($"{original.DeclaringType?.Name}.{original.Name} no longer reads the fluid layer through IBlockAccessor");
    }

    private static readonly object SolidTick = new();
}
