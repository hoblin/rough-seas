using System;
using System.Linq;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

public static class Boats
{
    private static readonly object gate = new();
    private static BoatClass[] classes = [new(typeof(EntityBoat))];
    private static event Action<BoatClass>? Added;

    public static BoatClass? Of(Entity? entity)
    {
        if (entity == null) return null;
        foreach (var boat in classes)
        {
            if (boat.Type.IsInstanceOfType(entity)) return boat;
        }
        return null;
    }

    public static bool Is(Entity? entity) => Of(entity) != null;

    public static void Add(Type type)
    {
        lock (gate)
        {
            if (classes.Any(boat => boat.Type == type)) return;
            var added = new BoatClass(type);
            classes = [.. classes, added];
            Added?.Invoke(added);
        }
    }

    public static void Remove(Type type)
    {
        lock (gate) classes = [classes[0], .. classes.Skip(1).Where(boat => boat.Type != type)];
    }

    /// <summary>
    /// Runs <paramref name="patch"/> on every copy of vanilla's boat, and on each added later, until disposed. A patch on
    /// vanilla's own <c>EntityBoat</c> is written out in full, where <c>bin/compat</c> reads it.
    /// </summary>
    public static IDisposable ReachCopies(Action<BoatClass> patch)
    {
        lock (gate)
        {
            foreach (var boat in classes.Skip(1)) patch(boat);
            Added += patch;
            return new Reached(patch);
        }
    }

    private sealed class Reached(Action<BoatClass> patch) : IDisposable
    {
        public void Dispose()
        {
            lock (gate) Added -= patch;
        }
    }
}
