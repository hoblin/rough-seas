using System;
using System.Collections.Generic;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

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
        return new VesselInWind(config, bare, SailOf(attributes["sail"], config, polar), (attributes["lateralCentreMetres"].AsDouble(), 0));
    }

    private static Sail? SailOf(JsonObject sail, WindConfig config, Func<string, SailPolar> polar)
    {
        if (!sail.Exists) return null;

        var form = new SailForm(Corner(sail, "luffFoot"), Corner(sail, "luffHead"), Corner(sail, "leechHead"), Corner(sail, "leechFoot"),
            Corner(sail, "pivot"), config.Camber);
        return new Sail(polar(sail["polar"].AsString()), form, sail["sailSquareMetres"].AsDouble(), sail["halfSquareMetres"].AsDouble(),
            (sail["halfReach"].AsDouble(1), sail["halfSpan"].AsDouble(1)), sail["leastTrimDegrees"].AsDouble() * GameMath.DEG2RAD,
            Sail.ClothNewtons(config.ClothKgPerSquareMetre));
    }

    private static HullPoint Corner(JsonObject sail, string name) => HullPoint.Of(sail[name + "Metres"].AsArray<double>());

    protected override IEnumerable<WindSurface> Surfaces(Func<WindSurface, Airflow> air)
    {
        if (sail != null && sail.Draws(air(sail)))
        {
            yield return sail;
            yield break;
        }

        sail?.Slack(air(sail));
        yield return bare;
    }
}
