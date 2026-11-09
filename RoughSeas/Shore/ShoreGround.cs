using System.Collections.Generic;
using RoughSeas.SeaAndWeather;
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
    private const double StillLevel = 7.0 / 8;

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
        if (OpenWater.Land(blocks, probe)) return Land(x, z, fluid.Id);
        if (Groundwater.Carries(fluid) && !blocks.GetBlock(probe.Up(), BlockLayersAccess.Fluid).IsLiquid())
        {
            return new ShoreCell(ShoreKind.Water, 0, beds.Of(fluid.BlockMaterial), fluid.Id);
        }
        return default;
    }

    private ShoreCell Land(int x, int z, int fluid)
    {
        var top = level;
        while (top < level + highestRise && OpenWater.Land(blocks, probe.Set(x, top + 1, z))) top++;
        var bed = beds.Of(blocks.GetBlock(probe.Set(x, top, z), BlockLayersAccess.Solid).BlockMaterial);
        return new ShoreCell(ShoreKind.Land, top + 1 - (level + StillLevel), bed, fluid);
    }
}
