using System;
using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.Cargo;

public readonly record struct Lading(int Slot, int Size, int MaxSize);

public readonly record struct Stowed(int Slot, int Place);

public static class Stowage
{
    public static double Fill(IReadOnlyList<Lading> lading, int slots) =>
        slots == 0 ? 0 : lading.Sum(stack => (double)stack.Size / stack.MaxSize) / slots;

    public static IEnumerable<Stowed> Lay(IReadOnlyList<Lading> lading, IReadOnlyList<double[]> places)
    {
        var middle = new double[3];
        foreach (var place in places)
            for (int axis = 0; axis < 3; axis++) middle[axis] += place[axis] / places.Count;

        var byNearness = Enumerable.Range(0, places.Count)
            .OrderBy(i => Math.Sqrt(Enumerable.Range(0, 3).Sum(axis => Math.Pow(places[i][axis] - middle[axis], 2))))
            .ThenBy(i => i);
        var largest = lading.OrderByDescending(stack => stack.Size).ThenBy(stack => stack.Slot);
        return largest.Zip(byNearness, (stack, place) => new Stowed(stack.Slot, place));
    }
}
