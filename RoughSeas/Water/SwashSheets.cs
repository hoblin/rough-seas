using System;
using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.Water;

public readonly record struct SwashSheet(double Distance, double RunUp, double Needed, int FromX, int FromZ, double Slope);

/// <summary>The sheets a shore's waves throw over land, at a range of the run-up's shares: the smallest share that reaches a block is the run-up a wave needs to wet it, and the steps that stop a sheet are kept with what each share brings to them.</summary>
public sealed class SwashSheets(Band band)
{
    public static readonly double[] Scales = [0.4, 0.55, 0.7, 0.85, 1.0, 1.15];
    private static readonly (int X, int Z)[] Sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public (Dictionary<(int X, int Z), SwashSheet> Sheets, List<StepFace> Steps) Throw(SeabedMap map, Func<int, int, Sea> seaAt)
    {
        var sheets = new Dictionary<(int X, int Z), SwashSheet>();
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
                    sheets[cell] = new SwashSheet(distance, reached.RunUp, scale * reached.RunUp, reached.FromX, reached.FromZ, 0);
                    RecordSteps(map, ground, level, cell, reached, distance, index, steps);
                }
            }
        }
        return (Sloped(sheets), steps.Values.ToList());
    }

    /// <summary>Each shore's sheets with the slope its run-up's front climbs to where it stops: the run-up over the farthest the full run-up's sheet reaches, so a front drawn up that slope stops where the front followed block by block does, on a flat beach as on a stepped one.</summary>
    private static Dictionary<(int X, int Z), SwashSheet> Sloped(Dictionary<(int X, int Z), SwashSheet> sheets)
    {
        var reach = new Dictionary<(int X, int Z), double>();
        foreach (var sheet in sheets.Values)
        {
            if (sheet.Needed > sheet.RunUp) continue;
            var from = (sheet.FromX, sheet.FromZ);
            reach[from] = Math.Max(reach.GetValueOrDefault(from), sheet.Distance + 0.5);
        }
        return sheets.ToDictionary(pair => pair.Key, pair => pair.Value with
        {
            Slope = pair.Value.RunUp / Math.Max(reach.GetValueOrDefault((pair.Value.FromX, pair.Value.FromZ)), 0.5),
        });
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
}
