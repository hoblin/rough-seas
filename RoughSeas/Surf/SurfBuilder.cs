using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.Shore;

namespace RoughSeas.Surf;

/// <summary>
/// Builds a <see cref="SurfField"/> from a seabed: the lags of each slot, and the band's sheets thrown at a range of the
/// run-up's shares, the smallest share that reaches a block being the run-up a wave needs to wet it, with the faces waves
/// meet and the steps that stop a sheet.
/// </summary>
public sealed class SurfBuilder(Band band, FaceBuilder faces)
{
    private const double StillLevel = 7.0 / 8;
    private const float StepFoot = -1.5f;
    public static readonly double[] Scales = [0.4, 0.55, 0.7, 0.85, 1.0, 1.15];
    private static readonly (int X, int Z)[] Sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public SurfField Build(SeabedMap map, Func<int, int, Sea> seaAt, (double X, double Z)[] numbers)
    {
        var size = map.Size;
        var field = new SurfField(map.OriginX, map.OriginZ, size, numbers);
        var depth = new float[size * size];
        for (var i = 0; i < depth.Length; i++) depth[i] = map.DepthAt(map.OriginX + i % size, map.OriginZ + i / size);

        var lags = numbers.Take(SurfField.Slots).Select(number => PhaseField.Lag(size, depth, number.X, number.Z)).ToArray();
        var (swash, steps) = Swash(map, seaAt);

        for (var i = 0; i < depth.Length; i++)
        {
            var (x, z) = (map.OriginX + i % size, map.OriginZ + i / size);
            var d = depth[i];
            field.Set(field.Bed, x, z, 0, d);
            field.Set(field.Aux, x, z, 0, (float)(map[x, z].Bed?.Conductivity ?? 0));
            field.Set(field.Aux, x, z, 1, Gradient(depth, size, i, 1));
            field.Set(field.Aux, x, z, 2, Gradient(depth, size, i, size));
            field.Set(field.Aux, x, z, 3, map[x, z].Kind == ShoreKind.Land ? (float)(map.LevelAt(x, z) + StillLevel + map[x, z].Elevation) : 0);

            var reached = swash.TryGetValue((x, z), out var sheet);
            field.Set(field.Bed, x, z, 1, reached ? (float)sheet.Distance : 0);
            field.Set(field.Bed, x, z, 2, reached ? (float)sheet.RunUp : 0);
            field.Set(field.Bed, x, z, 3, reached ? (float)sheet.Needed : d > 0 ? 0 : SurfField.Unreached);
            for (var slot = 0; slot < lags.Length; slot++)
            {
                var lag = reached
                    ? lags[slot][(sheet.FromZ - map.OriginZ) * size + sheet.FromX - map.OriginX] + numbers[slot].X * (sheet.FromX - x) + numbers[slot].Z * (sheet.FromZ - z)
                    : lags[slot][i];
                field.Set(field.Lags[slot / SurfField.Channels], x, z, slot % SurfField.Channels, (float)lag);
            }
        }
        field.Walls = faces.Build(map, seaAt, field);
        field.Steps = steps;
        foreach (var step in steps) field.Set(field.Face, step.X, step.Z, 0, StepFoot);
        return field;
    }

    private (Dictionary<(int X, int Z), Sheet> Sheets, List<StepFace> Steps) Swash(SeabedMap map, Func<int, int, Sea> seaAt)
    {
        var sheets = new Dictionary<(int X, int Z), Sheet>();
        var steps = new Dictionary<(int X, int Z, int SideX, int SideZ), StepFace>();
        var levels = new HashSet<int>();
        for (var x = map.OriginX; x < map.OriginX + map.Size; x++)
        for (var z = map.OriginZ; z < map.OriginZ + map.Size; z++)
        {
            if (map[x, z].Kind == ShoreKind.Land) levels.Add(map.LevelAt(x, z));
        }

        foreach (var level in levels)
        {
            var ground = map.Level(level);
            var seeds = Seeds(map, ground).ToList();
            if (seeds.Count == 0) continue;
            for (var index = Scales.Length - 1; index >= 0; index--)
            {
                var scale = Scales[index];
                foreach (var (cell, reached) in band.Spread(ground, seeds, (x, z) => [seaAt(x, z)], scale))
                {
                    var distance = Math.Max(0, Math.Sqrt((cell.X - reached.FromX) * (cell.X - reached.FromX) + (cell.Z - reached.FromZ) * (cell.Z - reached.FromZ)) - 0.5);
                    sheets[cell] = new Sheet(distance, reached.RunUp, scale * reached.RunUp, reached.FromX, reached.FromZ);
                    RecordSteps(map, ground, level, cell, reached, distance, index, steps);
                }
            }
        }
        return (sheets, steps.Values.ToList());
    }

    private static void RecordSteps(SeabedMap map, IShoreGround ground, int level, (int X, int Z) cell, Reached reached, double distance, int index,
        Dictionary<(int X, int Z, int SideX, int SideZ), StepFace> steps)
    {
        if (reached.Head <= 0) return;
        var from = ground.At(cell.X, cell.Z).Elevation;
        foreach (var (dx, dz) in Sides)
        {
            var (x, z) = (cell.X + dx, cell.Z + dz);
            var neighbour = ground.At(x, z);
            var rise = neighbour.Kind == ShoreKind.Land ? neighbour.Elevation - from : map.CrownAt(x, z) - (level + StillLevel) - from;
            if (double.IsNaN(rise) || rise <= 0 || rise < reached.Head) continue;
            if (!steps.TryGetValue((cell.X, cell.Z, dx, dz), out var step))
            {
                steps[(cell.X, cell.Z, dx, dz)] = step = new StepFace(cell.X, cell.Z, level, dx, dz, from, rise, distance, reached.RunUp, new double[Scales.Length], new double[Scales.Length]);
            }
            (step.Heads[index], step.Lenses[index]) = (reached.Head, reached.Lens);
        }
    }

    private static IEnumerable<ShoreSeed> Seeds(SeabedMap map, IShoreGround ground)
    {
        for (var x = map.OriginX; x < map.OriginX + map.Size; x++)
        for (var z = map.OriginZ; z < map.OriginZ + map.Size; z++)
        {
            var cell = ground.At(x, z);
            if (cell.Kind == ShoreKind.Water && Sides.Any(side => ground.At(x + side.X, z + side.Z).Kind == ShoreKind.Land)) yield return new ShoreSeed(x, z, cell.Fluid);
        }
    }

    private static float Gradient(float[] depth, int size, int i, int step)
    {
        var (column, row) = (i % size, i / size);
        var (back, ahead) = step == 1 ? (column > 0 ? i - 1 : i, column < size - 1 ? i + 1 : i) : (row > 0 ? i - size : i, row < size - 1 ? i + size : i);
        if (back == ahead || float.IsNaN(depth[back]) || float.IsNaN(depth[ahead])) return 0;
        return (depth[ahead] - depth[back]) / ((ahead - back) / step);
    }

    private readonly record struct Sheet(double Distance, double RunUp, double Needed, int FromX, int FromZ);
}
