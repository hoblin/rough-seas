using System.Collections.Generic;
using RoughSeas.SeaAndWeather;
using RoughSeas.Water;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace RoughSeas.Shore;

public enum ShoreKind
{
    Barrier,
    Water,
    Land,
}

/// <param name="Elevation">Land: its top over still water, in metres. Water: its depth where the reader measures it (<c>SeabedMap</c>), else 0.</param>
/// <param name="Fluid">The fluid block it holds: water's own, or the water under land.</param>
public readonly record struct ShoreCell(ShoreKind Kind, double Elevation, BedConfig Bed, int Fluid);

public interface IShoreGround
{
    ShoreCell At(int x, int z);
}

public sealed class ShoreGround(IBlockAccessor blocks, int level, Beds beds, int highestRise) : IShoreGround
{
    private readonly Dictionary<(int, int), ShoreCell> cells = [];
    private readonly BlockPos probe = new(Dimensions.NormalWorld);

    public int Level => level;

    public ShoreCell At(int x, int z)
    {
        if (cells.TryGetValue((x, z), out var known)) return known;
        var cell = Read(x, z);
        if (cell.Kind != ShoreKind.Barrier || Loaded(x, z)) cells[(x, z)] = cell;
        return cell;
    }

    private bool Loaded(int x, int z) => blocks.GetChunkAtBlockPos(probe.Set(x, level, z)) != null;

    private ShoreCell Read(int x, int z)
    {
        probe.Set(x, level, z);
        if (blocks.GetChunkAtBlockPos(probe) == null) return default;
        var fluid = blocks.GetBlock(probe, BlockLayersAccess.Fluid);
        if (OpenWater.Land(blocks, probe)) return LandAt(blocks, probe, x, level, z, fluid.Id, beds, highestRise);
        if (Groundwater.Carries(fluid) && !blocks.GetBlock(probe.Up(), BlockLayersAccess.Fluid).IsLiquid())
        {
            return new ShoreCell(ShoreKind.Water, 0, beds.Of(fluid.BlockMaterial), fluid.Id);
        }
        return default;
    }

    /// <summary>The top solid block of the ground standing at <paramref name="level"/>, no higher than <paramref name="highestRise"/> over it.</summary>
    public static int Crown(IBlockAccessor blocks, BlockPos probe, int x, int level, int z, int highestRise)
    {
        var top = level;
        while (top < level + highestRise && OpenWater.Land(blocks, probe.Set(x, top + 1, z))) top++;
        return top;
    }

    public static ShoreCell LandAt(IBlockAccessor blocks, BlockPos probe, int x, int level, int z, int fluid, Beds beds, int highestRise)
    {
        var top = Crown(blocks, probe, x, level, z, highestRise);
        var bed = beds.Of(blocks.GetBlock(probe.Set(x, top, z), BlockLayersAccess.Solid).BlockMaterial);
        return new ShoreCell(ShoreKind.Land, top + 1 - (level + WaterColumn.StillLevel), bed, fluid);
    }
}
