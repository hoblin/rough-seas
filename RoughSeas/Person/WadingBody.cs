using System;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;

namespace RoughSeas.Person;

/// <summary>
/// A body standing or jumping in water below its chest: vanilla's walk, jump and gravity move it, and across, the water
/// drags only the part of it under the surface, ½ ρ Cd A v² over that part's share of the body, toward the water's own velocity.
/// </summary>
public sealed class WadingBody(IPerson person, PersonConfig config, Waves waves)
{
    private readonly Body body = person.Body;

    /// <summary>Its motion across after the water's drag over this step, in blocks per tick.</summary>
    /// <param name="water">The water's velocity at the body, the current's and the waves', m/s.</param>
    public (double X, double Z) Dragged(WaterSurface surface, BodyPlace at, (double X, double Z) motion, Carried kit, double seconds, (double X, double Z) water)
    {
        var density = waves.DensityOf(surface.Salt);
        var waterline = surface.Height - at.Feet;
        var air = body.BreathingLitres;
        var share = body.LitresUnder(waterline, air) / body.Litres(air);
        var inertia = body.MassKg + kit.Kg + body.AddedAcrossKg(waterline, air, density);

        var (throughX, throughZ) = (motion.X * WaterDrag.TicksPerSecond - water.X, motion.Z * WaterDrag.TicksPerSecond - water.Z);
        var through = Math.Sqrt(throughX * throughX + throughZ * throughZ);
        var kept = through > 0 ? Body.Dragged(through, density, config.Body.DragAreaAcross * share, inertia, seconds) / through : 1;

        return ((water.X + throughX * kept) / WaterDrag.TicksPerSecond, (water.Z + throughZ * kept) / WaterDrag.TicksPerSecond);
    }
}
