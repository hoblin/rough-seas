using System;
using System.Collections.Generic;
using RoughSeas.SeaAndWeather;
using RoughSeas.Shore;
using RoughSeas.Water;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace RoughSeas.Surf;

/// <summary>
/// The columns around a point as the shore's water stands in them: a water column by its surface level and depth,
/// sounded block by block as deep as the shore looks and read from worldgen's seabed below that; a land column by the
/// water under it and the top of its ground. Read off the main thread, from height maps copied on it.
/// </summary>
public sealed class SeabedMap
{
    private const int Unknown = int.MinValue;
    private const double Deep = 1000;
    private const double DryLand = 1;
    private static readonly (int X, int Z)[] Sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    private readonly ShoreCell[] cells;
    private readonly int[] levels;
    private readonly double[] crowns;
    private readonly BedConfig?[] faces;

    public SeabedMap(int originX, int originZ, int size)
    {
        (OriginX, OriginZ, Size) = (originX, originZ, size);
        cells = new ShoreCell[size * size];
        levels = new int[size * size];
        crowns = new double[size * size];
        faces = new BedConfig?[size * size];
        Array.Fill(levels, Unknown);
        Array.Fill(crowns, double.NaN);
    }

    public int OriginX { get; }

    public int OriginZ { get; }

    public int Size { get; }

    public ShoreCell this[int x, int z] => Inside(x, z) ? cells[Index(x, z)] : default;

    public int LevelAt(int x, int z) => Inside(x, z) ? levels[Index(x, z)] : Unknown;

    public double CrownAt(int x, int z)
    {
        if (!Inside(x, z)) return double.NaN;
        return this[x, z] is { Kind: ShoreKind.Land } land ? LevelAt(x, z) + WaterColumn.StillLevel + land.Elevation : crowns[Index(x, z)];
    }

    public BedConfig? FaceBedAt(int x, int z) => !Inside(x, z) ? null : this[x, z] is { Kind: ShoreKind.Land } land ? land.Bed : faces[Index(x, z)];

    public float DepthAt(int x, int z) => this[x, z] switch
    {
        { Kind: ShoreKind.Water } water => (float)water.Elevation,
        { Kind: ShoreKind.Land } land => (float)-land.Elevation,
        { Kind: ShoreKind.Barrier, Elevation: > 0 } dry => (float)-dry.Elevation,
        _ => (float)Deep,
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
        var unpacked = new HashSet<(int X, int Y, int Z)>();
        for (var i = 0; i < size * size; i++)
        {
            var (x, z) = (originX + i % size, originZ + i / size);
            if (x < 0 || z < 0 || !heights.TryGetValue((x / chunk, z / chunk), out var column)) continue;
            var index = z % chunk * chunk + x % chunk;
            var top = (int)column.Rain[index];
            int terrain = column.Terrain[index];
            if (blocks.GetChunkAtBlockPos(probe.Set(x, top, z)) == null) continue;
            // The lock-free accessor reads a chunk packed after 8 idle seconds as air.
            var lowest = Math.Max(Math.Min(top - deepest, Math.Min(top, terrain + 1) - highestRise) - 1, 0);
            for (var section = lowest / chunk; section <= (top + highestRise + 1) / chunk; section++)
            {
                if (unpacked.Add((x / chunk, section, z / chunk))) blocks.GetChunkAtBlockPos(x, section * chunk, z)?.Unpack_ReadOnly();
            }
            map.Fill(i, blocks, probe, x, z, top, terrain, beds, highestRise, deepest);
        }
        map.ReadFaces(blocks, probe, beds, highestRise);
        return map;
    }

    public void Put(int x, int z, ShoreCell cell, int level) => (cells[Index(x, z)], levels[Index(x, z)]) = (cell, level);

    public void SetFace(int x, int z, double crown, BedConfig bed) => (crowns[Index(x, z)], faces[Index(x, z)]) = (crown, bed);

    private void ReadFaces(IBlockAccessor blocks, BlockPos probe, Beds beds, int highestRise)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            if (cells[i] is not ({ Kind: ShoreKind.Barrier, Elevation: > 0 } or { Kind: ShoreKind.Water })) continue;
            var (x, z) = (OriginX + i % Size, OriginZ + i / Size);
            var level = HighestLevelBeside(x, z);
            if (level == Unknown || (cells[i].Kind == ShoreKind.Water && level <= levels[i]) || !OpenWater.Land(blocks, probe.Set(x, level, z))) continue;
            var crown = ShoreGround.Crown(blocks, probe, x, level, z, highestRise);
            SetFace(x, z, crown + 1, beds.Of(blocks.GetBlock(probe.Set(x, crown, z), BlockLayersAccess.Solid).BlockMaterial));
        }
    }

    private int HighestLevelBeside(int x, int z)
    {
        var highest = Unknown;
        foreach (var (dx, dz) in Sides) highest = Math.Max(highest, LevelAt(x + dx, z + dz));
        return highest;
    }

    private void Fill(int i, IBlockAccessor blocks, BlockPos probe, int x, int z, int top, int terrain, Beds beds, int highestRise, int deepest)
    {
        for (var y = top; y >= Math.Min(top, terrain + 1) - highestRise; y--)
        {
            var fluid = blocks.GetBlock(probe.Set(x, y, z), BlockLayersAccess.Fluid);
            if (!Groundwater.Carries(fluid)) continue;
            (cells[i], levels[i]) = (OpenWater.Land(blocks, probe) ? ShoreGround.LandAt(blocks, probe, x, y, z, fluid.Id, beds, highestRise) : Water(blocks, probe, x, y, z, terrain, fluid, beds, deepest), y);
            return;
        }
        cells[i] = new ShoreCell(ShoreKind.Barrier, DryLand, null!, 0);
    }

    private static ShoreCell Water(IBlockAccessor blocks, BlockPos probe, int x, int top, int z, int terrain, Block fluid, Beds beds, int deepest)
    {
        var bottom = top;
        while (top - bottom < deepest && OpenWater.At(blocks, probe.Set(x, bottom - 1, z)).IsLiquid()) bottom--;
        var ground = blocks.GetBlock(probe.Set(x, bottom - 1, z), BlockLayersAccess.Solid);
        var sounded = top - bottom < deepest && ground.Id != 0;
        var known = sounded || terrain < top;
        var bed = beds.Of(ground.BlockMaterial);
        return new ShoreCell(ShoreKind.Water, known ? top + WaterColumn.StillLevel - (sounded ? bottom : terrain + 1) : Deep, bed, fluid.Id);
    }

    private int Index(int x, int z) => (z - OriginZ) * Size + (x - OriginX);

    private sealed class AtLevel(SeabedMap map, int level) : IShoreGround
    {
        public ShoreCell At(int x, int z) => map.LevelAt(x, z) == level ? map[x, z] : default;
    }
}
