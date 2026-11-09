using System;
using System.Collections.Generic;

namespace RoughSeas.Shore;

public readonly record struct Sea(double Height, double Period);

public readonly record struct ShoreSeed(int X, int Z, int Fluid);

/// <param name="Rings">How many blocks in from the shore's water the front came.</param>
/// <param name="Head">The front's velocity head on arriving, in metres.</param>
/// <param name="FromX">The cell of water the front was thrown from.</param>
/// <param name="RunUp">The run-up of the sea that threw it, before any scaling.</param>
public readonly record struct Reached(int Fluid, int Rings, double Head, int FromX, int FromZ, double RunUp);

/// <summary>
/// Where a shore's swash reaches: from each cell of water beside land, a sheet is thrown up the land by the run-up of the
/// sea there (<see cref="RunUp"/>), measured along the slope inland, and followed block by block (<see cref="SheetFront"/>),
/// the strongest front taking each cell first.
/// </summary>
public sealed class Band(RunUp runUp, SheetConfig sheet, int slopeSearchBlocks)
{
    private static readonly (int X, int Z)[] Sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public Dictionary<(int X, int Z), Reached> Spread(IShoreGround ground, IEnumerable<ShoreSeed> seeds, Func<int, int, IReadOnlyList<Sea>> seas,
        double runUpScale = 1)
    {
        var reached = new Dictionary<(int X, int Z), Reached>();
        var fronts = new PriorityQueue<(int X, int Z, Reached From, SheetFront Front), double>();

        foreach (var seed in seeds)
        {
            foreach (var (dx, dz) in Sides)
            {
                if (ground.At(seed.X + dx, seed.Z + dz) is not { Kind: ShoreKind.Land } land) continue;
                if (Thrown(ground, seed.X, seed.Z, dx, dz, seas(seed.X, seed.Z), runUpScale) is not var (runUp, thrown)) continue;
                if (thrown.Cross(land.Elevation, land.Bed) is not { } front) continue;
                fronts.Enqueue((seed.X + dx, seed.Z + dz, new Reached(seed.Fluid, 1, front.Head, seed.X, seed.Z, runUp), front), -front.Head);
            }
        }

        while (fronts.TryDequeue(out var next, out _))
        {
            if (reached.ContainsKey((next.X, next.Z))) continue;
            reached[(next.X, next.Z)] = next.From with { Head = next.Front.Head };
            var from = ground.At(next.X, next.Z);
            foreach (var (dx, dz) in Sides)
            {
                var (x, z) = (next.X + dx, next.Z + dz);
                if (reached.ContainsKey((x, z)) || ground.At(x, z) is not { Kind: ShoreKind.Land } land) continue;
                if (next.Front.Cross(land.Elevation - from.Elevation, land.Bed) is { } front)
                {
                    fronts.Enqueue((x, z, next.From with { Rings = next.From.Rings + 1 }, front), -front.Head);
                }
            }
        }
        return reached;
    }

    public double RunUpFrom(IShoreGround ground, ShoreSeed seed, Sea sea)
    {
        var strongest = 0.0;
        foreach (var (dx, dz) in Sides)
        {
            if (ground.At(seed.X + dx, seed.Z + dz).Kind != ShoreKind.Land) continue;
            strongest = Math.Max(strongest, runUp.Over(sea.Height, sea.Period, Profile(ground, seed.X, seed.Z, dx, dz), ground.At(seed.X + dx, seed.Z + dz).Bed));
        }
        return strongest;
    }

    private (double RunUp, SheetFront Front)? Thrown(IShoreGround ground, int x, int z, int dx, int dz, IReadOnlyList<Sea> seas, double runUpScale)
    {
        var profile = Profile(ground, x, z, dx, dz);
        var bed = ground.At(x + dx, z + dz).Bed;
        (double RunUp, SheetFront Front)? strongest = null;
        foreach (var sea in seas)
        {
            var reach = runUp.Over(sea.Height, sea.Period, profile, bed);
            var front = SheetFront.Thrown(reach * runUpScale, sea.Period, sheet);
            if (front.Head > (strongest?.Front.Head ?? 0)) strongest = (reach, front);
        }
        return strongest;
    }

    private List<double> Profile(IShoreGround ground, int x, int z, int dx, int dz)
    {
        var profile = new List<double>();
        for (var i = 1; i <= slopeSearchBlocks && ground.At(x + i * dx, z + i * dz) is { Kind: ShoreKind.Land } land; i++) profile.Add(land.Elevation);
        return profile;
    }
}
