using System;
using RoughSeas.Vessels;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Propulsion;

/// <summary>
/// Where her rower sits and what drives her from there: the tools she takes, how far a sweep reaches round her, how
/// high his hands are over the water, and her own oars when she has them. The water's depth under her comes from
/// <paramref name="depthWithin"/>, which counts whole metres down to a limit.
/// </summary>
public sealed class RowerStation(JsonObject attributes, PropulsionConfig config, double waterDensity, Func<int, double> depthWithin)
{
    public const string OwnOars = "tholeoar";

    private readonly string? paddle = attributes["paddle"].AsString();
    private readonly OarsConfig? oars = attributes["oars"].AsObject<OarsConfig?>(null);
    private readonly double sweepMetres = attributes["sweepMetres"].AsDouble();
    private readonly PolingConfig poling = attributes["pole"].AsObject<PolingConfig>(new());
    private readonly double abeamMetres = attributes["paddleAbeamMetres"].AsDouble();

    public bool HasOars => oars != null;

    public SeatPose? Stance(string? kind) => kind == Pole.Code ? poling.Stance?.Pose : null;

    public Propulsor? Propulsor(Tool? tool, out string? refusal)
    {
        if (tool != null) return tool.Drive(this, out refusal);
        return Oars(out refusal, "nothing in hand that moves her");
    }

    public Propulsor? Poled(Pole pole, out string? refusal)
    {
        var hands = poling.HandsAboveWaterMetres;
        var reach = pole.Reach(hands);
        var depth = depthWithin((int)Math.Ceiling(reach) + 1);
        refusal = depth > reach ? $"a {pole.Metres:F0} m pole reaches {reach:F1} m, the water is deeper" : null;
        return refusal == null ? new HeldTool(new PoleStroke(poling.Stroke, config.Strokes[poling.Stroke], pole.Kilograms, depth, hands), sweepMetres) : null;
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
