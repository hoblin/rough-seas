using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RoughSeas.Storms;
using RoughSeas.Water;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Soundscape;

public static class ShoreSound
{
    private const string HarmonyId = "roughseas.soundscape.shore";

    private static readonly ConditionalWeakTable<BlockSounds, ConcurrentDictionary<string, BlockSounds>> Swaps = new();
    // The engine's ambient scan runs the patched methods off the main thread: whatever these postfixes read must be safe there.
    [ThreadStatic] private static BlockPos? probe;
    [ThreadStatic] private static BlockPos? lastRead;
    [ThreadStatic] private static Shore? lastShore;
    private static Harmony? harmony;
    private static ShoreConfig config = new();
    private static StormsModSystem? seas;

    public static void Attach(ShoreConfig shore, StormsModSystem storms)
    {
        if (harmony != null) return;
        (config, seas) = (shore, storms);
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.DeclaredMethod(typeof(BlockWater), nameof(BlockWater.GetAmbientSoundStrength)),
            postfix: new HarmonyMethod(typeof(ShoreSound), nameof(Strength)));
        harmony.Patch(AccessTools.Method(typeof(Block), nameof(Block.GetSounds), [typeof(IBlockAccessor), typeof(BlockSelection), typeof(ItemStack)]),
            postfix: new HarmonyMethod(typeof(ShoreSound), nameof(Sounds)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        (harmony, seas) = (null, null);
    }

    private static void Strength(IWorldAccessor world, BlockPos pos, ref float __result)
    {
        if (Read(world.BlockAccessor, pos) is { Breaking: true } shore)
        {
            __result = (float)(Curve.At(config.BreakingStrength, shore.WaveHeight) * (SurfOf(shore.WaveHeight)?.Gain ?? 1));
        }
    }

    private static void Sounds(Block __instance, IBlockAccessor blockAccessor, BlockSelection blockSel, ref BlockSounds __result)
    {
        if (__instance is not BlockWater || __result == null || blockSel?.Position is not { } pos) return;
        if (Read(blockAccessor, pos) is { Breaking: true } shore && SurfOf(shore.WaveHeight) is { } surf)
        {
            __result = Swapped(__result, surf.Sound, config.SurfBlockCount);
        }
    }

    private static SurfBand? SurfOf(double waveHeight) => config.Surf.LastOrDefault(band => band.FromMetres <= waveHeight);

    private static BlockSounds Swapped(BlockSounds still, string ambient, float blockCount) =>
        Swaps.GetOrCreateValue(still).GetOrAdd(ambient, _ =>
        {
            var swapped = still.Clone();
            swapped.Ambient = new AssetLocation(ambient);
            swapped.AmbientBlockCount = blockCount;
            return swapped;
        });

    public static string Survey(IWorldAccessor world, BlockPos centre, int radius)
    {
        var accessor = world.BlockAccessor;
        var (lapping, breaking, highest) = (0, 0, 0.0);
        var (lappingStrength, breakingStrength) = (0.0, 0.0);
        var pos = new BlockPos(centre.dimension);
        for (var dx = -radius; dx <= radius; dx++)
        for (var dz = -radius; dz <= radius; dz++)
        {
            pos.Set(centre.X + dx, centre.Y + 3, centre.Z + dz);
            while (pos.Y > centre.Y - 8 && !OpenWater.At(accessor, pos).IsLiquid()) pos.Down();
            if (OpenWater.At(accessor, pos) is not BlockWater water) continue;
            if (Read(accessor, pos) is not { } shore) continue;

            highest = Math.Max(highest, shore.WaveHeight);
            var strength = water.GetAmbientSoundStrength(world, pos);
            var ambient = water.GetSounds(accessor, new BlockSelection { Position = pos })?.Ambient?.ToString();
            if (config.Surf.Any(band => new AssetLocation(band.Sound).ToString() == ambient)) (breaking, breakingStrength) = (breaking + 1, breakingStrength + strength);
            else if (strength > 0) (lapping, lappingStrength) = (lapping + 1, lappingStrength + strength);
        }
        return $"within {radius} blocks: {lapping} lapping at mean strength {(lapping > 0 ? lappingStrength / lapping : 0):F2}, " +
            $"{breaking} breaking at {(breaking > 0 ? breakingStrength / breaking : 0):F2}; waves up to {highest:F1} m";
    }

    private static Shore? Read(IBlockAccessor accessor, BlockPos pos)
    {
        if (lastRead != null && lastRead.Equals(pos))
        {
            lastRead = null;
            return lastShore;
        }
        lastShore = Measure(accessor, pos);
        (lastRead ??= new BlockPos(pos.dimension)).Set(pos);
        lastRead.dimension = pos.dimension;
        return lastShore;
    }

    private static Shore? Measure(IBlockAccessor accessor, BlockPos pos)
    {
        if (seas?.DrawnSea is not { } drawn) return null;

        var at = probe ??= new BlockPos(pos.dimension);
        at.Set(pos.X, pos.Y + 1, pos.Z);
        at.dimension = pos.dimension;
        if (accessor.GetBlock(at, BlockLayersAccess.Fluid).IsLiquid() || accessor.GetBlock(at, BlockLayersAccess.Solid).Id != 0) return null;

        var here = drawn.WaveHere(pos.X + 0.5, pos.Z + 0.5);
        return new Shore(here.Arriving, here.Surf);
    }

    private readonly record struct Shore(double WaveHeight, bool Breaking);
}
