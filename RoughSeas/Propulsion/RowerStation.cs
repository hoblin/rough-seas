using System;
using RoughSeas.Vessels;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Propulsion;

/// <summary>
/// Where her rower sits and what drives her from there: the tools she takes, how far a sweep reaches round her, how
/// high his hands are over the water, and her own oars when she has them. The water's depth at a point of her hull,
/// along and across her in metres, comes from <paramref name="depthAt"/>, which measures from the surface to the bed,
/// down to a limit in blocks. Unless <paramref name="deepWaterRefuses"/>, water deeper than a pole's full stroke is
/// poled as at its full stroke.
/// </summary>
public sealed class RowerStation(JsonObject attributes, PropulsionConfig config, double waterDensity, Func<double, double, int, double> depthAt, Func<bool> deepWaterRefuses)
{
    public const string OwnOars = "tholeoar";

    private readonly string? paddle = attributes["paddle"].AsString();
    private readonly OarsConfig? oars = attributes["oars"].AsObject<OarsConfig?>(null);
    private readonly double sweepMetres = attributes["sweepMetres"].AsDouble();
    private readonly PolingConfig poling = attributes["pole"].AsObject<PolingConfig>(new());
    private readonly double abeamMetres = attributes["paddleAbeamMetres"].AsDouble();

    public bool HasOars => oars != null;

    public SeatPose? Stance(string? kind) => kind == Pole.Code ? poling.Stance?.Pose : null;

    public double? BedMetres { get; private set; }

    public double? Slide(double poleMetres, double downward, double? drawnShare)
    {
        if (BedMetres is not { } bed || downward <= 0 || poling.DrawnPoleMetres <= 0) return null;
        var footed = Math.Min(bed, poleMetres * downward - poling.HandsAboveWaterMetres);
        var lifted = drawnShare is { } share ? config.Strokes[poling.Stroke].Lift(share) : 1;
        return (footed - poling.DrawnBedMetres) * (1 - lifted) / downward - (poleMetres - poling.DrawnPoleMetres);
    }

    public Propulsor? Propulsor(Tool? tool, int way, out string? refusal)
    {
        BedMetres = null;
        if (tool != null) return tool.Drive(this, way, out refusal);
        return Oars(out refusal, "nothing in hand that moves her");
    }

    public Propulsor? Poled(Pole pole, int way, out string? refusal)
    {
        var hands = poling.HandsAboveWaterMetres;
        var reach = pole.Reach(hands);
        var blocks = (int)Math.Ceiling(reach) + 1;
        var plant = way < 0 ? poling.PlantAsternMetres : poling.PlantAheadMetres;
        var foot = (double depth) => plant + Math.Sign(way) * pole.Footing(depth + hands);
        var sounded = (double depth) => depthAt(foot(depth), poling.PlantAcrossMetres, blocks);
        var first = sounded(depthAt(plant, poling.PlantAcrossMetres, blocks));
        var depth = Math.Max(first, sounded(first));
        if (!deepWaterRefuses()) depth = Math.Min(depth, pole.FullStrokeDepth(hands, config.Strokes[poling.Stroke].PushMetres));
        BedMetres = Math.Min(depth, reach);
        refusal = depth > reach ? $"a {pole.Metres:F0} m pole reaches {reach:F1} m, the water is deeper" : null;
        return refusal == null ? new HeldTool(new PoleStroke(poling.Stroke, config.Strokes[poling.Stroke], pole, depth, hands, waterDensity), sweepMetres) : null;
    }

    public Propulsor? Paddled(double bladeSquareMetres, out string? refusal)
    {
        refusal = null;
        return paddle != null ? new HeldTool(Blade(paddle, bladeSquareMetres), sweepMetres, abeamMetres) : Oars(out refusal, "a paddle doesn't serve her");
    }

    private Propulsor? Oars(out string? refusal, string without)
    {
        refusal = oars == null ? without : null;
        return oars == null ? null : new TholeOars(Blade(oars.Stroke, oars.BladeSquareMetres), oars.SpanMetres);
    }

    private BladeStroke Blade(string stroke, double squareMetres) => new(stroke, config.Strokes[stroke], squareMetres, waterDensity);
}

public class OarsConfig
{
    public string Stroke = "";
    public double BladeSquareMetres;
    public double SpanMetres;
}
