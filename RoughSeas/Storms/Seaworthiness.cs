using System;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Storms;

/// <summary>How a vessel takes the sea, from its <c>roughseas:seaforces</c> config.</summary>
/// <param name="Heave">The share of a crest's height the hull rises with: what is left climbs its side.</param>
/// <param name="RollResponse">How many times the water's slope across it it rolls, as drawn; vanilla's roll where no swell is drawn.</param>
/// <param name="PitchResponse">How many times the water's slope along it it pitches, as drawn; vanilla's pitch where no swell is drawn.</param>
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

    /// <summary>
    /// How she takes the sea with her bilge this full, from 0 to 1: water aboard settles her until, full, her sides
    /// are awash, and its free surface makes her roll more and go over sooner.
    /// </summary>
    public Seaworthiness Laden(double fill, SwampingConfig swamping) => this with
    {
        FreeboardMetres = FreeboardMetres * (1 - fill),
        CapsizeBreakerMetres = CapsizeBreakerMetres * (1 - swamping.FreeSurfaceCapsize * fill),
        RollResponse = RollResponse * (1 + swamping.FreeSurfaceRoll * fill)
    };

    public double Overtopping(double crest) => Math.Max(0, crest * (1 - Heave) - FreeboardMetres);

    public double ShippedShare(double crest)
    {
        var overtopping = Overtopping(crest);
        return overtopping > 0 ? overtopping / (crest * (1 - Heave)) : 0;
    }

    public bool Capsizes(double breakerMetres) => breakerMetres >= CapsizeBreakerMetres;
}
