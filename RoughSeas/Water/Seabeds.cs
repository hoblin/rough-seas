using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Common;

namespace RoughSeas.Water;

/// <summary>The seabeds the server reads around the bodies it draws the sea for, one an area of <c>surface.shore.moveBlocks</c>, shared by every body in it.</summary>
public sealed class Seabeds(Func<SurfMaker> maker, ShoreSurfConfig config, IWorldAccessor world)
{
    private const double ForgottenAfterSeconds = 60;

    private readonly Dictionary<(int X, int Z), (SurfMaker Maker, double ReadAt)> makers = [];
    private double sweptAt;

    public SurfField? At(ISeaSource map, double x, double z, (double X, double Z)[] numbers, double[] amplitudeShares)
    {
        var now = world.ElapsedMilliseconds / 1000.0;
        if (now - sweptAt > ForgottenAfterSeconds) Sweep(now);
        var area = ((int)Math.Floor(x / config.MoveBlocks), (int)Math.Floor(z / config.MoveBlocks));
        var reader = makers.TryGetValue(area, out var known) ? known.Maker : maker();
        makers[area] = (reader, now);
        reader.Update(world, map, x, z, numbers, amplitudeShares, now);
        return reader.Field;
    }

    private void Sweep(double now)
    {
        sweptAt = now;
        foreach (var area in makers.Where(known => now - known.Value.ReadAt > ForgottenAfterSeconds).Select(known => known.Key).ToList()) makers.Remove(area);
    }
}
