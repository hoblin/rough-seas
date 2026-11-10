using System;
using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.Water;

/// <summary>Assembles a <see cref="SurfField"/> from a seabed: what the bed does to the waves, the sheets they throw over land and the faces they meet.</summary>
public sealed class SurfBuilder(SwashSheets sheets, FaceBuilder faces, WaveTransformation transformation)
{
    /// <param name="dominant">The slot that dominates the sea now; the arriving sea and the whole sea's gain and breaking follow it.</param>
    /// <param name="coarse">The wider bed that gives every reader of this place one reference for the waves' lag.</param>
    public SurfField Build(SeabedMap map, Func<int, int, Sea> seaAt, (double X, double Z)[] numbers, double[]? amplitudeShares = null, int dominant = 0, CoarseBed? coarse = null)
    {
        var size = map.Size;
        var field = new SurfField(map.OriginX, map.OriginZ, size, numbers);
        var depth = new float[size * size];
        for (var i = 0; i < depth.Length; i++) depth[i] = map.DepthAt(map.OriginX + i % size, map.OriginZ + i / size);

        var heights = Enumerable.Range(0, depth.Length).Select(i => seaAt(map.OriginX + i % size, map.OriginZ + i / size).Height).ToArray();
        var entering = coarse?.Entering(transformation, numbers, amplitudeShares, dominant, map.OriginX, map.OriginZ);
        var arrivingWaves = transformation.Solve(size, depth, numbers, amplitudeShares, heights, dominant, entering);
        var ray = dominant < numbers.Length ? numbers[dominant] : default;
        var arriving = Enumerable.Range(0, depth.Length).Select(i => depth[i] > 0 ? (float)arrivingWaves.ArrivingAt(depth, size, i % size, i / size, ray) : 1f).ToArray();
        var (swash, steps) = sheets.Throw(map, (x, z) => seaAt(x, z) is var sea ? sea with { Height = sea.Height * arriving[(z - map.OriginZ) * size + x - map.OriginX] } : default);
        var waves = transformation.Solve(size, depth, numbers, amplitudeShares, heights, dominant, entering, shoreBore: ShoreBore(map, size, swash.Values));

        var shoreline = PhaseField.NearestWater(depth, size);
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

    /// <summary>At a shoreline a bore and the swash it throws move as one (Shen &amp; Meyer 1963): the significant wave's sheet leaves at U = √(2 g R) for its run-up R, so the bore reaching that shoreline runs over a depth of U² / g. Each cell takes its nearest shoreline's.</summary>
    private static float[] ShoreBore(SeabedMap map, int size, IEnumerable<SwashSheet> sheets)
    {
        var runUps = new float[size * size];
        foreach (var sheet in sheets)
        {
            var i = (sheet.FromZ - map.OriginZ) * size + sheet.FromX - map.OriginX;
            runUps[i] = Math.Max(runUps[i], (float)(sheet.RunUp / Math.Sqrt(WaveTheory.TwoPercentRatio)));
        }
        var nearest = PhaseField.NearestWater(runUps, size);
        return nearest.Select(shore => shore >= 0 ? 2 * runUps[shore] : 0f).ToArray();
    }

}
