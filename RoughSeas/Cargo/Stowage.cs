using System;
using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.Cargo;

public readonly record struct Load(int Slot, int Size, int MaxSize);

public readonly record struct Stowed(int Slot, int Place);

public static class Stowage
{
    public static double Fill(IReadOnlyList<Load> loads, int slots) =>
        slots == 0 ? 0 : loads.Sum(load => (double)load.Size / load.MaxSize) / slots;

    public static IEnumerable<Stowed> Lay(IReadOnlyList<Load> loads, IReadOnlyList<double[]> places)
    {
        var middle = new double[3];
        foreach (var place in places)
            for (int axis = 0; axis < 3; axis++) middle[axis] += place[axis] / places.Count;

        var byNearness = Enumerable.Range(0, places.Count)
            .OrderBy(i => Math.Sqrt(Enumerable.Range(0, 3).Sum(axis => Math.Pow(places[i][axis] - middle[axis], 2))))
            .ThenBy(i => i);
        var largest = loads.OrderByDescending(load => load.Size).ThenBy(load => load.Slot);
        return largest.Zip(byNearness, (load, place) => new Stowed(load.Slot, place));
    }
}
