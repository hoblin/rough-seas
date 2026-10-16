using System;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Storms;

/// <summary>How a vessel takes the sea, from its <c>roughseas:seaforces</c> config.</summary>
/// <param name="Heave">The share of a crest's height the hull rises with: what is left climbs its side.</param>
/// <param name="RollResponse">How many times vanilla's roll it rolls, as drawn.</param>
/// <param name="PitchResponse">How many times vanilla's pitch it pitches, as drawn.</param>
public readonly record struct Seaworthiness(
    double FreeboardMetres,
    double Heave,
    double CapsizeBreakerMetres,
    double RollResponse,
    double PitchResponse)
{
    public static Seaworthiness From(JsonObject attributes) => new(
        attributes["freeboardMetres"].AsDouble(),
        attributes["heave"].AsDouble(),
        attributes["capsizeBreakerMetres"].AsDouble(double.PositiveInfinity),
        attributes["rollResponse"].AsDouble(1),
        attributes["pitchResponse"].AsDouble(1));

    public double Overtopping(double crest) => Math.Max(0, crest * (1 - Heave) - FreeboardMetres);

    public double ShippedShare(double crest)
    {
        var overtopping = Overtopping(crest);
        return overtopping > 0 ? overtopping / (crest * (1 - Heave)) : 0;
    }

    public bool Capsizes(double breakerMetres) => breakerMetres >= CapsizeBreakerMetres;
}
