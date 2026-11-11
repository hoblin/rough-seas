using System;
using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.SeaAndWeather;

/// <summary>The storms that passed over one place in a window, with how much of the window one stood over it and the longest stretch none did.</summary>
public sealed class StormSurvey
{
    public StormSurvey(IEnumerable<StormPass> passes, double fromHours, double toHours)
    {
        Passes = [.. passes.OrderBy(pass => pass.FromHours)];
        Hours = toHours - fromHours;
        var calmUntil = fromHours;
        foreach (var pass in Passes)
        {
            LongestCalmHours = Math.Max(LongestCalmHours, pass.FromHours - calmUntil);
            CoveredHours += Math.Max(0, pass.ToHours - Math.Max(pass.FromHours, calmUntil));
            calmUntil = Math.Max(calmUntil, pass.ToHours);
        }
        LongestCalmHours = Math.Max(LongestCalmHours, toHours - calmUntil);
    }

    public IReadOnlyList<StormPass> Passes { get; }

    public double Hours { get; }

    public double CoveredHours { get; }

    public double LongestCalmHours { get; }

    public IEnumerable<(string Code, int Count)> ByType =>
        Passes.GroupBy(pass => pass.Storm.Code).Select(group => (group.Key, group.Count()));
}
