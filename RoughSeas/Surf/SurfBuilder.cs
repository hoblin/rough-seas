using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.Shore;
using RoughSeas.Water;

namespace RoughSeas.Surf;

/// <summary>Assembles a <see cref="SurfField"/> from a seabed: what the bed does to the waves, the sheets they throw over land and the faces they meet.</summary>
public sealed class SurfBuilder(SwashSheets sheets, FaceBuilder faces, WaveTransformation transformation)
{
    private static readonly (int X, int Z)[] Sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public SurfField Build(SeabedMap map, Func<int, int, Sea> seaAt, (double X, double Z)[] numbers, double[]? amplitudeShares = null)
    {
        var size = map.Size;
        var field = new SurfField(map.OriginX, map.OriginZ, size, numbers);
        var depth = new float[size * size];
        for (var i = 0; i < depth.Length; i++) depth[i] = map.DepthAt(map.OriginX + i % size, map.OriginZ + i / size);

        var waves = transformation.Solve(size, depth, numbers, amplitudeShares, Enumerable.Range(0, depth.Length).Select(i => seaAt(map.OriginX + i % size, map.OriginZ + i / size).Height).ToArray());
        var ray = Strongest(numbers, amplitudeShares);
        var (swash, steps) = sheets.Throw(map, (x, z) => seaAt(x, z) is var sea ? sea with { Height = sea.Height * waves.ArrivingAt(depth, size, x - map.OriginX, z - map.OriginZ, ray) } : default);

        var shoreline = NearestWater(depth, size);
        for (var i = 0; i < depth.Length; i++)
        {
            var (x, z) = (map.OriginX + i % size, map.OriginZ + i / size);
            field.SetDepth(x, z, waves.Felt[i]);
            field.SetConductivity(x, z, (float)(map[x, z].Bed?.Conductivity ?? 0));
            field.SetSlope(x, z, waves.SlopeX[i], waves.SlopeZ[i]);
            if (map[x, z].Kind == ShoreKind.Land) field.SetGroundTop(x, z, (float)(map.LevelAt(x, z) + WaterColumn.StillLevel + map[x, z].Elevation));
            else field.SetBreaking(x, z, waves.Breaking[i]);

            var reached = swash.TryGetValue((x, z), out var sheet);
            field.SetSheet(x, z, (float)sheet.Distance, (float)sheet.RunUp, reached ? (float)sheet.Needed : depth[i] > 0 ? 0 : SurfField.Unreached);
            for (var slot = 0; slot < waves.Lags.Length; slot++)
            {
                var near = shoreline[i];
                var carried = near >= 0 && near != i;
                var (nearX, nearZ) = near >= 0 ? (map.OriginX + near % size, map.OriginZ + near / size) : (x, z);
                field.SetLag(slot, x, z, carried ? (float)(waves.Lags[slot][near] + numbers[slot].X * (nearX - x) + numbers[slot].Z * (nearZ - z)) : waves.Lags[slot][i]);
                field.SetGain(slot, x, z, waves.Gains[slot][carried ? near : i]);
            }
        }
        field.BoreDepth = waves.BoreDepth;
        field.Walls = faces.Build(map, (x, z) => seaAt(x, z) is var sea ? sea with { Height = sea.Height * waves.Gain[(z - map.OriginZ) * size + x - map.OriginX] } : default, field);
        field.Steps = steps;
        foreach (var step in steps) field.SetFaceDistance(step.X, step.Z, SurfField.StepFootDistance);
        return field;
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
}
