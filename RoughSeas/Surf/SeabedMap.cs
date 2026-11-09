using System;
using System.Collections.Generic;
using RoughSeas.SeaAndWeather;
using RoughSeas.Shore;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace RoughSeas.Surf;

/// <summary>
/// The columns around a point as the shore's water stands in them: a water column by its surface level and depth,
/// worldgen's seabed trusted while water still reaches it; a land column by the water under it and the top of its
/// ground. Read off the main thread, from height maps copied on it.
/// </summary>
public sealed class SeabedMap
{
    private const double StillLevel = 7.0 / 8;
    private const int Unknown = int.MinValue;
    private const double Deep = 1000;

    private readonly ShoreCell[] cells;
    private readonly int[] levels;

    public SeabedMap(int originX, int originZ, int size)
    {
        (OriginX, OriginZ, Size) = (originX, originZ, size);
        cells = new ShoreCell[size * size];
        levels = new int[size * size];
        Array.Fill(levels, Unknown);
    }

    public int OriginX { get; }

    public int OriginZ { get; }

    public int Size { get; }

    public ShoreCell this[int x, int z] => Inside(x, z) ? cells[Index(x, z)] : default;

    public int LevelAt(int x, int z) => Inside(x, z) ? levels[Index(x, z)] : Unknown;

    public float DepthAt(int x, int z) => this[x, z] switch
    {
        { Kind: ShoreKind.Water } water => (float)water.Elevation,
        { Kind: ShoreKind.Land } land => (float)-land.Elevation,
        _ => float.NaN,
    };

    public bool Inside(int x, int z) => x >= OriginX && z >= OriginZ && x < OriginX + Size && z < OriginZ + Size;

    public IShoreGround Level(int level) => new AtLevel(this, level);

    public static Dictionary<(int X, int Z), (ushort[] Rain, ushort[] Terrain)> Heights(IBlockAccessor blocks, int originX, int originZ, int size)
    {
        var heights = new Dictionary<(int X, int Z), (ushort[] Rain, ushort[] Terrain)>();
        const int chunk = GlobalConstants.ChunkSize;
        for (var chunkX = Math.DivRem(originX, chunk, out _) - 1; chunkX * chunk < originX + size; chunkX++)
        for (var chunkZ = Math.DivRem(originZ, chunk, out _) - 1; chunkZ * chunk < originZ + size; chunkZ++)
        {
            if (blocks.GetMapChunk(chunkX, chunkZ) is { } map) heights[(chunkX, chunkZ)] = ((ushort[])map.RainHeightMap.Clone(), (ushort[])map.WorldGenTerrainHeightMap.Clone());
        }
        return heights;
    }

    public static SeabedMap Read(IBlockAccessor blocks, Dictionary<(int X, int Z), (ushort[] Rain, ushort[] Terrain)> heights, int originX, int originZ, int size,
        Beds beds, int highestRise, int deepest)
    {
        const int chunk = GlobalConstants.ChunkSize;
        var map = new SeabedMap(originX, originZ, size);
        var probe = new BlockPos(Dimensions.NormalWorld);
        for (var i = 0; i < size * size; i++)
        {
            var (x, z) = (originX + i % size, originZ + i / size);
            if (!heights.TryGetValue((x / chunk, z / chunk), out var column)) continue;
            var index = z % chunk * chunk + x % chunk;
            var top = (int)column.Rain[index];
            if (blocks.GetChunkAtBlockPos(probe.Set(x, top, z)) == null) continue;
            map.Fill(i, blocks, probe, x, z, top, column.Terrain[index], beds, highestRise, deepest);
        }
        return map;
    }

    private void Fill(int i, IBlockAccessor blocks, BlockPos probe, int x, int z, int top, int terrain, Beds beds, int highestRise, int deepest)
    {
        var fluid = blocks.GetBlock(probe.Set(x, top, z), BlockLayersAccess.Fluid);
        if (Groundwater.Carries(fluid) && !OpenWater.Land(blocks, probe))
        {
            var worldgen = terrain < top && OpenWater.At(blocks, probe.Set(x, terrain + 1, z)).IsLiquid();
            var bottom = worldgen ? terrain + 1 : top;
            while (!worldgen && top - bottom < deepest && OpenWater.At(blocks, probe.Set(x, bottom - 1, z)).IsLiquid()) bottom--;
            var bed = beds.Of(blocks.GetBlock(probe.Set(x, bottom - 1, z), BlockLayersAccess.Solid).BlockMaterial);
            var depth = worldgen || top - bottom < deepest ? top + StillLevel - bottom : Deep;
            (cells[i], levels[i]) = (new ShoreCell(ShoreKind.Water, depth, bed, fluid.Id), top);
            return;
        }

        var ground = Math.Min(top, terrain + 1);
        for (var y = ground; y >= ground - highestRise; y--)
        {
            var held = blocks.GetBlock(probe.Set(x, y, z), BlockLayersAccess.Fluid);
            if (!Groundwater.Carries(held) || !OpenWater.Land(blocks, probe)) continue;
            var crown = y;
            while (crown < y + highestRise && OpenWater.Land(blocks, probe.Set(x, crown + 1, z))) crown++;
            var bed = beds.Of(blocks.GetBlock(probe.Set(x, crown, z), BlockLayersAccess.Solid).BlockMaterial);
            (cells[i], levels[i]) = (new ShoreCell(ShoreKind.Land, crown + 1 - (y + StillLevel), bed, held.Id), y);
            return;
        }
    }

    private int Index(int x, int z) => (z - OriginZ) * Size + (x - OriginX);

    private sealed class AtLevel(SeabedMap map, int level) : IShoreGround
    {
        public ShoreCell At(int x, int z) => map.LevelAt(x, z) == level ? map[x, z] : default;
    }
}
