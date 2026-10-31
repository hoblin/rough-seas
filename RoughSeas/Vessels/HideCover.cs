using System;
using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.Vessels;

/// <summary>
/// The hides a hull is covered with: small hides' worth of each <c>type</c>, and the seams between them. Each type keeps
/// for so many days wet at <see cref="HidesConfig.KeepCelsius"/>, then rots through in as long again, and takes up its
/// share of water.
/// </summary>
public sealed record HideCover(IReadOnlyDictionary<string, int> Sections, int Seams)
{
    public int Total => Sections.Values.Sum();

    public double SoakedKg(HidesConfig hides) =>
        Sections.Sum(entry => entry.Value * hides.SquareMetresASection * hides.KilogramsASquareMetre * hides.Of(entry.Key).Uptake);

    /// <summary>The share of the cover that rots as its exposure grows from <paramref name="before"/> to <paramref name="after"/>, each type no further than its own share.</summary>
    public double Rot(HidesConfig hides, double before, double after) =>
        Total == 0 ? 0 : Sections.Sum(entry => (double)entry.Value / Total * (Rotted(hides, entry.Key, after) - Rotted(hides, entry.Key, before)));

    private static double Rotted(HidesConfig hides, string stage, double exposure)
    {
        double keep = hides.Of(stage).KeepDays;
        return Math.Clamp((exposure - keep) / keep, 0, 1);
    }
}

public sealed class HidesConfig
{
    public double SquareMetresASection;
    public double KilogramsASquareMetre;
    public double KeepCelsius;
    public double KeepQ10;
    public Dictionary<string, HideStage> Stages = [];

    public HideStage Of(string stage) => Stages.TryGetValue(stage, out var known) ? known : Stages["raw"];

    public double DryDays;

    /// <summary><paramref name="days"/> wet in water at <paramref name="waterCelsius"/>, as days wet at <see cref="KeepCelsius"/>.</summary>
    public double Exposure(double days, double waterCelsius) => days * Math.Pow(KeepQ10, (waterCelsius - KeepCelsius) / 10);
}

public sealed class HideStage
{
    public double KeepDays;
    public double Uptake;
}
