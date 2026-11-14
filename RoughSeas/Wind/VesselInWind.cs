using System;
using System.Collections.Generic;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RoughSeas.Wind;

public sealed class VesselInWind(WindConfig config, BluffSurface bare, BluffSurface underSail, Sail? sail, (double Along, double Across) pivot) : BodyInWind(config)
{
    public Sail? Sail => sail;

    protected override (double Along, double Across) Pivot => pivot;

    public static VesselInWind From(JsonObject attributes, WindConfig config, Func<string, SailPolar> polar)
    {
        var bare = Bluff(attributes, config);
        var cloth = attributes["sail"]["sailSquareMetres"].AsDouble() * config.ClothKgPerSquareMetre;
        return new VesselInWind(config, bare, attributes["set"].Exists ? Bluff(attributes["set"], config) : bare, SailOf(attributes["sail"], Rig.Of(attributes["rig"], config.Camber, cloth), config, polar),
            (attributes["lateralCentreMetres"].AsDouble(), 0));
    }

    private static BluffSurface Bluff(JsonObject attributes, WindConfig config)
    {
        var centre = attributes["windageCentreMetres"].AsArray<double>([0, 0]);
        return new BluffSurface(attributes["windageSquareMetres"].AsDouble(), attributes["frontSquareMetres"].AsDouble(), config.BluffDrag, centre[0], 0, centre[1]);
    }

    private static Sail? SailOf(JsonObject sail, Rig? rig, WindConfig config, Func<string, SailPolar> polar) =>
        sail.Exists && rig != null
            ? new Sail(polar(sail["polar"].AsString() ?? throw new InvalidOperationException("a sail names no polar")),
                polar(config.Aback) as IncidencePolar ?? throw new InvalidOperationException($"the aback polar '{config.Aback}' gives no incidences"),
                rig, sail["sailSquareMetres"].AsDouble(), sail["halfSquareMetres"].AsDouble(),
                (sail["halfReach"].AsDouble(1), sail["halfSpan"].AsDouble(1)), Sail.ClothNewtons(config.ClothKgPerSquareMetre), config.Camber, config.Crew)
            : null;

    protected override IEnumerable<WindSurface> Surfaces(Func<WindSurface, Airflow> air, double seconds)
    {
        yield return sail is { Rolled: false } ? underSail : bare;
        if (sail == null) yield break;

        if (sail.Draws(air(sail))) yield return sail;
        else sail.Slack(seconds);
    }
}
