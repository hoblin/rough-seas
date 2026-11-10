using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.Shore;
using RoughSeas.Water;

namespace RoughSeas.Surf;

/// <summary>
/// Builds a <see cref="SurfField"/> from a seabed: the lags of each slot, and the band's sheets thrown at a range of the
/// run-up's shares, the smallest share that reaches a block being the run-up a wave needs to wet it, with the faces waves
/// meet and the steps that stop a sheet.
/// </summary>
public sealed class SurfBuilder(Band band, FaceBuilder faces, WaveTransformation transformation)
{
    public static readonly double[] Scales = [0.4, 0.55, 0.7, 0.85, 1.0, 1.15];
    private static readonly (int X, int Z)[] Sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public SurfField Build(SeabedMap map, Func<int, int, Sea> seaAt, (double X, double Z)[] numbers, double[]? amplitudeShares = null)
    {
        var size = map.Size;
        var field = new SurfField(map.OriginX, map.OriginZ, size, numbers);
        var depth = new float[size * size];
        for (var i = 0; i < depth.Length; i++) depth[i] = map.DepthAt(map.OriginX + i % size, map.OriginZ + i / size);

        var waves = transformation.Solve(size, depth, numbers, amplitudeShares, Enumerable.Range(0, depth.Length).Select(i => seaAt(map.OriginX + i % size, map.OriginZ + i / size).Height).ToArray());
        var ray = Strongest(numbers, amplitudeShares);
        var (swash, steps) = Swash(map, (x, z) => seaAt(x, z) is var sea ? sea with { Height = sea.Height * Arriving(depth, waves.Breaking, waves.Gain, size, x - map.OriginX, z - map.OriginZ, ray) } : default);

        var shoreline = NearestWater(depth, size);
        for (var i = 0; i < depth.Length; i++)
        {
            var (x, z) = (map.OriginX + i % size, map.OriginZ + i / size);
            var d = depth[i];
            field.SetDepth(x, z, waves.Felt[i]);
            field.SetConductivity(x, z, (float)(map[x, z].Bed?.Conductivity ?? 0));
            field.SetSlope(x, z, waves.SlopeX[i], waves.SlopeZ[i]);
            if (map[x, z].Kind == ShoreKind.Land) field.SetGroundTop(x, z, (float)(map.LevelAt(x, z) + WaterColumn.StillLevel + map[x, z].Elevation));
            else field.SetBreaking(x, z, waves.Breaking[i]);

            var reached = swash.TryGetValue((x, z), out var sheet);
            field.SetSheet(x, z, (float)sheet.Distance, (float)sheet.RunUp, reached ? (float)sheet.Needed : d > 0 ? 0 : SurfField.Unreached);
            for (var slot = 0; slot < waves.Lags.Length; slot++)
            {
                var near = shoreline[i];
                var carried = near >= 0 && near != i;
                var (nearX, nearZ) = near >= 0 ? (map.OriginX + near % size, map.OriginZ + near / size) : (x, z);
                field.SetLag(slot, x, z, carried ? (float)(waves.Lags[slot][near] + numbers[slot].X * (nearX - x) + numbers[slot].Z * (nearZ - z)) : waves.Lags[slot][i]);
                field.SetGain(slot, x, z, waves.Gains[slot][carried ? near : i]);
            }
        }
        field.Walls = faces.Build(map, seaAt, field);
        field.Steps = steps;
        foreach (var step in steps) field.SetFaceDistance(step.X, step.Z, SurfField.StepFootDistance);
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

    private void RecordSteps(SeabedMap map, IShoreGround ground, int level, (int X, int Z) cell, Reached reached, double distance, int index,
        Dictionary<(int X, int Z, int SideX, int SideZ), StepFace> steps)
    {
        if (reached.Head <= 0) return;
        var from = ground.At(cell.X, cell.Z).Elevation;
        foreach (var (dx, dz) in Sides)
        {
            var (x, z) = (cell.X + dx, cell.Z + dz);
            var neighbour = ground.At(x, z);
            var rise = neighbour.Kind == ShoreKind.Land ? neighbour.Elevation - from : map.CrownAt(x, z) - (level + WaterColumn.StillLevel) - from;
            if (double.IsNaN(rise) || rise <= 0 || (rise < reached.Head && !band.IsWall(rise, reached.Rise))) continue;
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

    private static int[] NearestWater(float[] depth, int size)
    {
        var nearest = new int[depth.Length];
        Array.Fill(nearest, -1);
        var waiting = new Queue<int>();
        for (var i = 0; i < depth.Length; i++)
        {
            if (depth[i] <= 0) continue;
            nearest[i] = i;
            waiting.Enqueue(i);
        }
        while (waiting.TryDequeue(out var i))
        {
            var (x, z) = (i % size, i / size);
            foreach (var (dx, dz) in Sides)
            {
                var (nextX, nextZ) = (x + dx, z + dz);
                if (nextX < 0 || nextZ < 0 || nextX >= size || nextZ >= size || nearest[nextZ * size + nextX] >= 0) continue;
                nearest[nextZ * size + nextX] = nearest[i];
                waiting.Enqueue(nextZ * size + nextX);
            }
        }
        return nearest;
    }

    private static (double X, double Z) Strongest((double X, double Z)[] numbers, double[]? amplitudeShares)
    {
        var strongest = default((double X, double Z));
        var most = double.NegativeInfinity;
        for (var slot = 0; slot < Math.Min(numbers.Length, SurfField.Slots); slot++)
        {
            var share = amplitudeShares?[slot] ?? 1;
            if (numbers[slot] == default || share <= most) continue;
            (strongest, most) = (numbers[slot], share);
        }
        return strongest;
    }

    /// <summary>The waves' height, against the deep sea's, where they enter the last stretch of breaking water before this shore: the sea that comes to the beach, whatever the shallows then leave of it.</summary>
    private static double Arriving(float[] depth, float[] breaking, float[] gain, int size, int column, int row, (double X, double Z) ray)
    {
        var length = Math.Sqrt(ray.X * ray.X + ray.Z * ray.Z);
        if (length <= 0) return 1;
        var last = -1;
        for (var step = 0; step < 2 * size; step++)
        {
            var (back, up) = ((int)Math.Round(column - ray.X / length * step), (int)Math.Round(row - ray.Z / length * step));
            if (back < 0 || up < 0 || back >= size || up >= size) return 1;
            var i = up * size + back;
            if (!(depth[i] > 0)) return last >= 0 ? Math.Min(1, gain[last]) : 1;
            if (breaking[i] <= 0) return Math.Min(1, gain[i]);
            last = i;
        }
        return 1;
    }

    private readonly record struct Sheet(double Distance, double RunUp, double Needed, int FromX, int FromZ);
}
