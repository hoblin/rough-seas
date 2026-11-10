using System;
using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.Water;

/// <summary>Assembles a <see cref="SurfField"/> from a seabed: what the bed does to the waves, the sheets they throw over land and the faces they meet.</summary>
public sealed class SurfBuilder(SwashSheets sheets, FaceBuilder faces, WaveTransformation transformation)
{
    private static readonly (int X, int Z)[] Sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <param name="dominant">The slot that dominates the sea now; the arriving sea and the whole sea's gain and breaking follow it.</param>
    public SurfField Build(SeabedMap map, Func<int, int, Sea> seaAt, (double X, double Z)[] numbers, double[]? amplitudeShares = null, int dominant = 0)
    {
        var size = map.Size;
        var field = new SurfField(map.OriginX, map.OriginZ, size, numbers);
        var depth = new float[size * size];
        for (var i = 0; i < depth.Length; i++) depth[i] = map.DepthAt(map.OriginX + i % size, map.OriginZ + i / size);

        var waves = transformation.Solve(size, depth, numbers, amplitudeShares, Enumerable.Range(0, depth.Length).Select(i => seaAt(map.OriginX + i % size, map.OriginZ + i / size).Height).ToArray(), dominant);
        var ray = dominant < numbers.Length ? numbers[dominant] : default;
        var arriving = Enumerable.Range(0, depth.Length).Select(i => depth[i] > 0 ? (float)waves.ArrivingAt(depth, size, i % size, i / size, ray) : 1f).ToArray();
        var (swash, steps) = sheets.Throw(map, (x, z) => seaAt(x, z) is var sea ? sea with { Height = sea.Height * arriving[(z - map.OriginZ) * size + x - map.OriginX] } : default);

        var shoreline = NearestWater(depth, size);
        for (var i = 0; i < depth.Length; i++)
        {
            var (x, z) = (map.OriginX + i % size, map.OriginZ + i / size);
            var reached = swash.TryGetValue((x, z), out var sheet);
            var near = reached ? (sheet.FromZ - map.OriginZ) * size + sheet.FromX - map.OriginX : shoreline[i];
            field.SetArriving(x, z, arriving[near >= 0 ? near : i]);
            field.SetDepth(x, z, waves.Felt[i]);
            field.SetCelerityDepth(x, z, waves.CelerityDepth[near >= 0 ? near : i]);
            field.SetConductivity(x, z, (float)(map[x, z].Bed?.Conductivity ?? 0));
            field.SetSlope(x, z, waves.SlopeX[i], waves.SlopeZ[i]);
            if (map[x, z].Kind == ShoreKind.Land) field.SetGroundTop(x, z, (float)(map.LevelAt(x, z) + WaterColumn.StillLevel + map[x, z].Elevation));
            else field.SetBreaking(x, z, waves.Breaking[i]);

            field.SetSheet(x, z, (float)sheet.Distance, (float)sheet.RunUp, reached ? (float)sheet.Needed : depth[i] > 0 ? 0 : SurfField.Unreached);
            for (var slot = 0; slot < waves.Lags.Length; slot++)
            {
                var carried = near >= 0 && near != i;
                var (nearX, nearZ) = near >= 0 ? (map.OriginX + near % size, map.OriginZ + near / size) : (x, z);
                field.SetLag(slot, x, z, carried ? (float)(waves.Lags[slot][near] + numbers[slot].X * (nearX - x) + numbers[slot].Z * (nearZ - z)) : waves.Lags[slot][i]);
                field.SetGain(slot, x, z, waves.Gains[slot][carried ? near : i]);
            }
        }
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
}
