using System;
using RoughSeas.Storms;
using RoughSeas.SeaAndWeather;

namespace RoughSeas.Person;

/// <summary>
/// A body standing or jumping in water below its chest: vanilla's walk, jump and gravity move it, and the water drags only
/// the part of it under the surface, ½ ρ Cd A v² over that part's share of the body, relative to the current.
/// </summary>
public sealed class WadingBody(IPerson person, PersonConfig config, Waves waves)
{
    private readonly Body body = person.Body;

    /// <summary>Its motion after the water's drag over this step, in blocks per tick.</summary>
    public BodyMotion Dragged(WaterSurface surface, BodyPlace at, BodyMotion motion, Carried kit, double seconds, (double X, double Z) current)
    {
        var density = waves.DensityOf(surface.Salt);
        var waterline = surface.Height - at.Feet;
        var air = body.BreathingLitres;
        var share = body.LitresUnder(waterline, air) / body.Litres(air);
        var massKg = body.MassKg + kit.Kg;

        var (acrossX, acrossZ) = (motion.X * WaterDrag.TicksPerSecond - current.X, motion.Z * WaterDrag.TicksPerSecond - current.Z);
        var across = Math.Sqrt(acrossX * acrossX + acrossZ * acrossZ);
        var keptAcross = across > 0
            ? Body.Dragged(across, density, config.Body.DragAreaAcross * share, massKg + body.AddedAcrossKg(waterline, air, density), seconds) / across
            : 1;
        var up = Body.Dragged(motion.Y * WaterDrag.TicksPerSecond, density, config.Body.DragAreaAlong * share, massKg + body.AddedKg(waterline, air, density), seconds);

        return new BodyMotion(
            (current.X + acrossX * keptAcross) / WaterDrag.TicksPerSecond,
            up / WaterDrag.TicksPerSecond,
            (current.Z + acrossZ * keptAcross) / WaterDrag.TicksPerSecond);
    }
}
