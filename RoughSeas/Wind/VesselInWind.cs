using System;
using System.Collections.Generic;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Wind;

public sealed class VesselInWind(WindConfig config, BluffSurface bare, Sail? sail, (double Along, double Across) pivot) : BodyInWind(config)
{
    public Sail? Sail => sail;

    protected override (double Along, double Across) Pivot => pivot;

    public static VesselInWind From(JsonObject attributes, WindConfig config, Func<string, SailPolar> polar)
    {
        var centre = attributes["windageCentreMetres"].AsArray<double>([0, 0]);
        var bare = new BluffSurface(attributes["windageSquareMetres"].AsDouble(), attributes["frontSquareMetres"].AsDouble(), config.BluffDrag,
            centre[0], 0, centre[1]);
        return new VesselInWind(config, bare, SailOf(attributes["sail"], polar), (attributes["lateralCentreMetres"].AsDouble(), 0));
    }

    private static Sail? SailOf(JsonObject sail, Func<string, SailPolar> polar)
    {
        if (!sail.Exists) return null;

        var tack = sail["tackMetres"].AsArray<double>();
        return new Sail(polar(sail["polar"].AsString()), sail["sailSquareMetres"].AsDouble(), sail["halfSquareMetres"].AsDouble(),
            tack[0], tack[1], sail["reachMetres"].AsDouble(), sail["heightMetres"].AsDouble());
    }

    protected override IEnumerable<WindSurface> Surfaces(Func<WindSurface, Airflow> air)
    {
        if (sail != null && sail.Draws(air(sail)))
        {
            yield return sail;
            yield break;
        }

        sail?.Slack();
        yield return bare;
    }
}
