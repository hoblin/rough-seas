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
    private const float DeepestFelt = 200;
    private const float ShallowestFelt = 0.05f;
    private const int Banks = 2;
    public static readonly double[] Scales = [0.4, 0.55, 0.7, 0.85, 1.0, 1.15];
    private static readonly (int X, int Z)[] Sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public SurfField Build(SeabedMap map, Func<int, int, Sea> seaAt, (double X, double Z)[] numbers, double[]? amplitudeShares = null, double breakingDissipation = 1)
    {
        var size = map.Size;
        var field = new SurfField(map.OriginX, map.OriginZ, size, numbers);
        var depth = new float[size * size];
        for (var i = 0; i < depth.Length; i++) depth[i] = map.DepthAt(map.OriginX + i % size, map.OriginZ + i / size);

        var smoothing = faces.SmoothingBlocks(LongestWave(numbers));
        var felt = Felt(depth, size, smoothing);
        var felts = numbers.Take(SurfField.Slots).Select(number => Felt(depth, size, faces.SmoothingBlocks(WaveLength(number)))).ToArray();
        var lags = felts.Select((each, slot) => PhaseField.Lag(size, each, numbers[slot].X, numbers[slot].Z)).ToArray();
        var (breaking, gain) = Energy(field, felt, felts, depth, size, numbers, lags, amplitudeShares, seaAt, breakingDissipation);
        var ray = Strongest(numbers, amplitudeShares);
        var (swash, steps) = Swash(map, (x, z) => seaAt(x, z) is var sea ? sea with { Height = sea.Height * Arriving(depth, breaking, gain, size, x - map.OriginX, z - map.OriginZ, ray) } : default);
        var slopeX = Averaged(Enumerable.Range(0, depth.Length).Select(i => Gradient(depth, size, i, 1)).ToArray(), size, smoothing);
        var slopeZ = Averaged(Enumerable.Range(0, depth.Length).Select(i => Gradient(depth, size, i, size)).ToArray(), size, smoothing);

        for (var i = 0; i < depth.Length; i++)
        {
            var (x, z) = (map.OriginX + i % size, map.OriginZ + i / size);
            var d = depth[i];
            field.Set(field.Bed, x, z, 0, felt[i]);
            field.Set(field.Aux, x, z, 0, (float)(map[x, z].Bed?.Conductivity ?? 0));
            field.Set(field.Aux, x, z, 1, slopeX[i]);
            field.Set(field.Aux, x, z, 2, slopeZ[i]);
            field.Set(field.Aux, x, z, 3, map[x, z].Kind == ShoreKind.Land ? (float)(map.LevelAt(x, z) + StillLevel + map[x, z].Elevation) : breaking[i]);

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
                if (reached) field.Set(field.Gains[slot / SurfField.Channels], x, z, slot % SurfField.Channels, field.Get(field.Gains[slot / SurfField.Channels], sheet.FromX, sheet.FromZ, slot % SurfField.Channels));
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

    private void RecordSteps(SeabedMap map, IShoreGround ground, int level, (int X, int Z) cell, Reached reached, double distance, int index,
        Dictionary<(int X, int Z, int SideX, int SideZ), StepFace> steps)
    {
        if (reached.Head <= 0) return;
        var from = ground.At(cell.X, cell.Z).Elevation;
        foreach (var (dx, dz) in Sides)
        {
            var (x, z) = (cell.X + dx, cell.Z + dz);
            var neighbour = ground.At(x, z);
            var rise = neighbour.Kind == ShoreKind.Land ? neighbour.Elevation - from : map.CrownAt(x, z) - (level + StillLevel) - from;
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

    private static double WaveLength((double X, double Z) number)
    {
        var size = Math.Sqrt(number.X * number.X + number.Z * number.Z);
        return size > 0 ? 2 * Math.PI / size : 0;
    }

    private static double LongestWave((double X, double Z)[] numbers) => numbers.Select(WaveLength).DefaultIfEmpty(0).Max();

    private static float[] Felt(float[] depth, int size, int radius)
    {
        var averaged = Averaged(depth.Select(d => float.IsNaN(d) ? 0 : Math.Clamp(d, 0, DeepestFelt)).ToArray(), size, radius);
        return depth.Select((d, i) => d > 0 ? Math.Max(averaged[i], ShallowestFelt) : d).ToArray();
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

    private (float[] Breaking, float[] Gain) Energy(SurfField field, float[] felt, float[][] felts, float[] depth, int size, (double X, double Z)[] numbers, float[][] lags, double[]? amplitudeShares, Func<int, int, Sea> seaAt, double dissipation)
    {
        var breaking = new float[size * size];
        var gain = new float[size * size];
        var heights = Enumerable.Range(0, size * size).Select(i => seaAt(field.OriginX + i % size, field.OriginZ + i / size).Height).ToArray();
        var seaHeight = heights.Max();
        var seaShare = heights.Select(height => seaHeight > 0 ? (float)(height / seaHeight) : 1f).ToArray();
        var generation = amplitudeShares == null ? numbers.Length : numbers.Length / Banks;
        for (var bank = 0; bank * generation < Math.Min(numbers.Length, SurfField.Slots); bank++)
        {
            var slots = Enumerable.Range(bank * generation, generation).Where(slot => slot < lags.Length).ToArray();
            var trains = slots.Select(slot => new WaveEnergy.Train(numbers[slot].X, numbers[slot].Z, (amplitudeShares?[slot] ?? 1 / (2 * Math.Sqrt(2) * Math.Sqrt(generation))) * seaHeight, lags[slot], felts[slot])).ToArray();
            var (gains, broken) = WaveEnergy.Solve(size, felt, trains, faces.BreakingDepthRatio, dissipation, depth, seaShare);
            var deep = trains.Sum(train => train.Amplitude * train.Amplitude);
            for (var i = 0; i < broken.Length; i++)
            {
                breaking[i] = Math.Max(breaking[i], broken[i]);
                if (deep > 0 && slots.Any(slot => numbers[slot] != default)) gain[i] = Math.Max(gain[i], (float)Math.Sqrt(trains.Select((train, index) => train.Amplitude * train.Amplitude * gains[index][i] * gains[index][i]).Sum() / deep));
                for (var train = 0; train < slots.Length; train++)
                {
                    var slot = slots[train];
                    field.Set(field.Gains[slot / SurfField.Channels], field.OriginX + i % size, field.OriginZ + i / size, slot % SurfField.Channels, numbers[slot] == default ? 1 : gains[train][i]);
                }
            }
        }
        return (breaking, gain);
    }

    private static float[] Averaged(float[] values, int size, int radius)
    {
        if (radius <= 0) return values;
        var across = size + 1;
        var totals = new double[across * across];
        for (var row = 0; row < size; row++)
        for (var column = 0; column < size; column++)
        {
            totals[(row + 1) * across + column + 1] = values[row * size + column] + totals[row * across + column + 1] + totals[(row + 1) * across + column] - totals[row * across + column];
        }

        var averaged = new float[values.Length];
        for (var row = 0; row < size; row++)
        for (var column = 0; column < size; column++)
        {
            var (left, right, top, bottom) = (Math.Max(column - radius, 0), Math.Min(column + radius + 1, size), Math.Max(row - radius, 0), Math.Min(row + radius + 1, size));
            var sum = totals[bottom * across + right] - totals[top * across + right] - totals[bottom * across + left] + totals[top * across + left];
            averaged[row * size + column] = (float)(sum / ((right - left) * (bottom - top)));
        }
        return averaged;
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
