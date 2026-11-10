using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.Shore;
using RoughSeas.Water;

namespace RoughSeas.Surf;

/// <summary>Finds every side of a full block a water column meets, the short steps of a beach and the tall faces of a cliff alike, and writes how each sends waves back into the <see cref="SurfField"/>.</summary>
public sealed class FaceBuilder(WallReflection reflection, FacesConfig config, double breakingDepthRatio)
{
    private const double OvertoppedNormal = 0.5;
    private const double BlendShareOfDistance = 0.5;
    private static readonly (int X, int Z)[] Sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];
    private static readonly (int X, int Z)[] Around = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    public int SmoothingBlocks(double wavelength) => Math.Min(config.SmoothingMaxBlocks, (int)Math.Ceiling(wavelength * config.SmoothingWavelengths));

    public IReadOnlyList<WallFace> Build(SeabedMap map, Func<int, int, Sea> seaAt, SurfField field)
    {
        var seeds = Seeds(map);
        var runs = Runs(seeds);
        var normals = new Normals(map, seeds);
        var walls = new List<WallFace>(seeds.Count);
        for (var i = 0; i < seeds.Count; i++)
        {
            var seed = seeds[i];
            var sea = seaAt(seed.X, seed.Z);
            var wavelength = WaveTheory.DeepLength(sea.Period);
            var height = Math.Min(sea.Height, map[seed.X, seed.Z].Elevation / breakingDepthRatio);
            var (normalX, normalZ, faceX, faceZ) = normals.Around(seed, SmoothingBlocks(wavelength));
            var coefficient = reflection.Coefficient(height, sea.Period, Slope(map, seed, height), seed.Bed.Roughness);
            walls.Add(new WallFace(seed.X, seed.Z, seed.Level, seed.SideX, seed.SideZ, normalX, normalZ, seed.Rise, runs[i], wavelength, coefficient,
                seed.Rise >= height * (1 + coefficient) / 2, faceX, faceZ));
        }
        Spread(map, walls, field);
        field.FaceSlope = config.ReflectedSlopeLimit;
        return walls;
    }

    private static List<Seed> Seeds(SeabedMap map)
    {
        var seeds = new List<Seed>();
        for (var x = map.OriginX; x < map.OriginX + map.Size; x++)
        for (var z = map.OriginZ; z < map.OriginZ + map.Size; z++)
        {
            if (map[x, z].Kind != ShoreKind.Water) continue;
            var level = map.LevelAt(x, z);
            foreach (var (dx, dz) in Sides)
            {
                var crown = map.CrownAt(x + dx, z + dz);
                if (double.IsNaN(crown) || map.FaceBedAt(x + dx, z + dz) is not { } bed) continue;
                var rise = crown - (level + WaterColumn.StillLevel);
                if (rise > 0) seeds.Add(new Seed(x, z, level, dx, dz, rise, bed));
            }
        }
        return seeds;
    }

    private static int[] Runs(List<Seed> seeds)
    {
        var byCell = new Dictionary<(int, int), List<int>>();
        for (var i = 0; i < seeds.Count; i++)
        {
            if (!byCell.TryGetValue((seeds[i].X, seeds[i].Z), out var list)) byCell[(seeds[i].X, seeds[i].Z)] = list = [];
            list.Add(i);
        }

        var runs = new int[seeds.Count];
        var seen = new HashSet<(int, int)>();
        var pending = new Stack<(int X, int Z)>();
        foreach (var start in byCell.Keys)
        {
            if (!seen.Add(start)) continue;
            var members = new List<int>();
            pending.Push(start);
            while (pending.TryPop(out var cell))
            {
                members.AddRange(byCell[cell]);
                foreach (var (dx, dz) in Around)
                {
                    var next = (cell.X + dx, cell.Z + dz);
                    if (byCell.ContainsKey(next) && seen.Add(next)) pending.Push(next);
                }
            }
            foreach (var member in members) runs[member] = members.Count;
        }
        return runs;
    }

    private double Slope(SeabedMap map, Seed seed, double height)
    {
        var toe = config.ToeDepthWaveHeights * height;
        var depth = Math.Min(map[seed.X, seed.Z].Elevation, toe);
        var run = 0.5;
        for (var i = 1; i < config.ReachBlocks && depth < toe; i++)
        {
            var (x, z) = (seed.X - i * seed.SideX, seed.Z - i * seed.SideZ);
            if (map[x, z].Kind != ShoreKind.Water) break;
            if (map[x, z].Elevation > depth) (depth, run) = (Math.Min(map[x, z].Elevation, toe), i + 0.5);
        }
        return (depth + Math.Min(seed.Rise, height)) / run;
    }

    private void Spread(SeabedMap map, List<WallFace> walls, SurfField field)
    {
        var nearest = new int[map.Size * map.Size];
        var distance = new float[map.Size * map.Size];
        Array.Fill(nearest, -1);
        Array.Fill(distance, float.PositiveInfinity);
        int Cell(int x, int z) => (z - map.OriginZ) * map.Size + x - map.OriginX;

        var queue = new PriorityQueue<(int X, int Z, int Wall), float>();
        for (var i = 0; i < walls.Count; i++) Offer(queue, walls[i].X, walls[i].Z, i);
        while (queue.TryDequeue(out var next, out var far))
        {
            var cell = Cell(next.X, next.Z);
            if (far >= distance[cell] && nearest[cell] >= 0) continue;
            (distance[cell], nearest[cell]) = (far, next.Wall);
            foreach (var (dx, dz) in Around)
            {
                var (x, z) = (next.X + dx, next.Z + dz);
                if (map[x, z].Kind == ShoreKind.Water && nearest[Cell(x, z)] < 0) Offer(queue, x, z, next.Wall);
            }
        }

        for (var i = 0; i < nearest.Length; i++)
        {
            var (x, z) = (map.OriginX + i % map.Size, map.OriginZ + i / map.Size);
            if (map[x, z].Kind != ShoreKind.Water)
            {
                field.SetFaceDistance(x, z, SurfField.LandDistance);
                continue;
            }
            if (nearest[i] < 0)
            {
                field.SetFaceDistance(x, z, config.ReachBlocks);
                continue;
            }
            var wall = walls[nearest[i]];
            var fromWall = (x + 0.5 - wall.FaceX) * wall.NormalX + (z + 0.5 - wall.FaceZ) * wall.NormalZ;
            var length = wall.StandsAboveCrest ? 1 : OvertoppedNormal;
            field.SetFace(x, z, (float)Math.Max(fromWall, FaceReading.FirstWaterVertex), (float)(wall.NormalX * length), (float)(wall.NormalZ * length),
                (float)(wall.Reflection * WallReflection.Spreading(wall.Run, wall.Wavelength, distance[i]) * reflection.Taper(distance[i])));
        }
        Blend(map, field, nearest, distance);

        void Offer(PriorityQueue<(int, int, int), float> into, int x, int z, int wall)
        {
            var (faceX, faceZ) = (walls[wall].X + 0.5 + 0.5 * walls[wall].SideX, walls[wall].Z + 0.5 + 0.5 * walls[wall].SideZ);
            var (dx, dz) = (x + 0.5 - faceX, z + 0.5 - faceZ);
            var far = (float)Math.Sqrt(dx * dx + dz * dz);
            if (far <= config.ReachBlocks) into.Enqueue((x, z, wall), far);
        }
    }

    private void Blend(SeabedMap map, SurfField field, int[] nearest, float[] distance)
    {
        var size = map.Size;
        var across = size + 1;
        var (weights, totals) = (new double[across * across], Enumerable.Range(0, 4).Select(_ => new double[across * across]).ToArray());
        for (var row = 0; row < size; row++)
        for (var column = 0; column < size; column++)
        {
            var (x, z, reached) = (map.OriginX + column, map.OriginZ + row, nearest[row * size + column] >= 0);
            var (here, up, left, corner) = ((row + 1) * across + column + 1, row * across + column + 1, (row + 1) * across + column, row * across + column);
            weights[here] = (reached ? 1 : 0) + weights[up] + weights[left] - weights[corner];
            var face = reached ? field.FaceOf(x, z) : default;
            float[] values = [face.Distance, face.NormalX, face.NormalZ, face.Reflection];
            for (var channel = 0; channel < totals.Length; channel++)
            {
                totals[channel][here] = values[channel] + totals[channel][up] + totals[channel][left] - totals[channel][corner];
            }
        }

        for (var row = 0; row < size; row++)
        for (var column = 0; column < size; column++)
        {
            var i = row * size + column;
            if (nearest[i] < 0) continue;
            var (x, z) = (map.OriginX + column, map.OriginZ + row);
            var radius = Math.Min(config.SmoothingMaxBlocks, (int)(distance[i] * BlendShareOfDistance));
            if (radius <= 0) continue;
            var (lower, upper, top, bottom) = (Math.Max(column - radius, 0), Math.Min(column + radius + 1, size), Math.Max(row - radius, 0), Math.Min(row + radius + 1, size));
            double Box(double[] table) => table[bottom * across + upper] - table[top * across + upper] - table[bottom * across + lower] + table[top * across + lower];
            var count = Box(weights);
            var (normalX, normalZ) = (Box(totals[1]), Box(totals[2]));
            var turned = Math.Sqrt(normalX * normalX + normalZ * normalZ);
            var before = field.FaceOf(x, z);
            var kept = Math.Sqrt(before.NormalX * before.NormalX + before.NormalZ * before.NormalZ);
            field.SetFace(x, z, (float)Math.Max(Box(totals[0]) / count, FaceReading.FirstWaterVertex),
                turned > 0 ? (float)(normalX / turned * kept) : before.NormalX, turned > 0 ? (float)(normalZ / turned * kept) : before.NormalZ,
                (float)(Box(totals[3]) / count));
        }
    }

    private sealed class Normals
    {
        private readonly SeabedMap map;
        private readonly double[][] counts, faceXs, faceZs;

        public Normals(SeabedMap map, List<Seed> seeds)
        {
            this.map = map;
            var across = map.Size + 1;
            double[][] Tables() => [new double[across * across], new double[across * across], new double[across * across], new double[across * across]];
            (counts, faceXs, faceZs) = (Tables(), Tables(), Tables());
            foreach (var seed in seeds)
            {
                var (side, i) = (Array.IndexOf(Sides, (seed.SideX, seed.SideZ)), (seed.Z - map.OriginZ + 1) * across + seed.X - map.OriginX + 1);
                counts[side][i]++;
                faceXs[side][i] += seed.X + 0.5 + 0.5 * seed.SideX;
                faceZs[side][i] += seed.Z + 0.5 + 0.5 * seed.SideZ;
            }
            foreach (var table in new[] { counts, faceXs, faceZs })
            foreach (var sum in table)
            {
                for (var row = 1; row < across; row++)
                for (var column = 1; column < across; column++)
                {
                    var i = row * across + column;
                    sum[i] += sum[i - 1] + sum[i - across] - sum[i - across - 1];
                }
            }
        }

        public (double X, double Z, double FaceX, double FaceZ) Around(Seed seed, int radius)
        {
            var back = Array.IndexOf(Sides, (seed.SideX, seed.SideZ)) ^ 1;
            double x = 0, z = 0, total = 0, faceX = 0, faceZ = 0;
            for (var side = 0; side < Sides.Length; side++)
            {
                if (side == back) continue;
                var count = Count(counts[side], seed, radius);
                (x, z, total) = (x - Sides[side].X * count, z - Sides[side].Z * count, total + count);
                (faceX, faceZ) = (faceX + Count(faceXs[side], seed, radius), faceZ + Count(faceZs[side], seed, radius));
            }
            var length = Math.Sqrt(x * x + z * z);
            return length > 0 ? (x / length, z / length, faceX / total, faceZ / total) : (-seed.SideX, -seed.SideZ, seed.X + 0.5 + 0.5 * seed.SideX, seed.Z + 0.5 + 0.5 * seed.SideZ);
        }

        private double Count(double[] sum, Seed seed, int radius)
        {
            var across = map.Size + 1;
            var (left, right) = (Math.Max(seed.X - map.OriginX - radius, 0), Math.Min(seed.X - map.OriginX + radius + 1, map.Size));
            var (top, bottom) = (Math.Max(seed.Z - map.OriginZ - radius, 0), Math.Min(seed.Z - map.OriginZ + radius + 1, map.Size));
            return sum[bottom * across + right] - sum[top * across + right] - sum[bottom * across + left] + sum[top * across + left];
        }
    }

    private readonly record struct Seed(int X, int Z, int Level, int SideX, int SideZ, double Rise, BedConfig Bed);
}
