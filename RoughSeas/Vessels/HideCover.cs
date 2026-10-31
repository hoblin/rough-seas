using System;
using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.Vessels;

public sealed record HideCover(IReadOnlyDictionary<string, int> Sections, int Seams)
{
    public int Total => Sections.Values.Sum();

    public double SoakedKg(HidesConfig hides) =>
        Sections.Sum(entry => entry.Value * hides.SquareMetresASection * hides.KilogramsASquareMetre * hides.Of(entry.Key).Uptake);

    public double Rot(HidesConfig hides, double wetDays, double days, double waterCelsius) =>
        Total == 0 ? 0 : Sections.Sum(entry =>
        {
            double keep = hides.KeepDays(entry.Key, waterCelsius);
            return (double)entry.Value / Total * Math.Clamp(wetDays - keep, 0, days) / keep;
        });
}

public sealed class HidesConfig
{
    public double SquareMetresASection;
    public double KilogramsASquareMetre;
    public double KeepCelsius;
    public double KeepQ10;
    public Dictionary<string, HideStage> Stages = [];

    public HideStage Of(string stage) => Stages.TryGetValue(stage, out var known) ? known : Stages["raw"];

    public double KeepDays(string stage, double waterCelsius) => Of(stage).KeepDays * Math.Pow(KeepQ10, (KeepCelsius - waterCelsius) / 10);
}

public sealed class HideStage
{
    public double KeepDays;
    public double Uptake;
}
