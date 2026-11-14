using System;
using System.Collections.Generic;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Wind;

public sealed class VesselInWind(WindConfig config, BluffSurface bare, BluffSurface set, IAloft? aloft, (double Along, double Across) pivot) : BodyInWind(config)
{
    protected override (double Along, double Across) Pivot => pivot;

    public static VesselInWind From(JsonObject attributes, WindConfig config, IAloft? aloft = null)
    {
        var bare = Bluff(attributes, config);
        return new VesselInWind(config, bare, attributes["set"].Exists ? Bluff(attributes["set"], config) : bare, aloft,
            (attributes["lateralCentreMetres"].AsDouble(), 0));
    }

    private static BluffSurface Bluff(JsonObject attributes, WindConfig config)
    {
        var centre = attributes["windageCentreMetres"].AsArray<double>([0, 0]);
        return new BluffSurface(attributes["windageSquareMetres"].AsDouble(), attributes["frontSquareMetres"].AsDouble(), config.BluffDrag, centre[0], 0, centre[1]);
    }

    protected override IEnumerable<WindSurface> Surfaces(Func<WindSurface, Airflow> air, double seconds)
    {
        yield return aloft is { Rolled: false } ? set : bare;
        if (aloft == null) yield break;

        foreach (var surface in aloft.Surfaces(air, seconds)) yield return surface;
    }
}
