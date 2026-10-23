using System;
using RoughSeas.SeaAndWeather;

namespace RoughSeas.Storms;

/// <summary>
/// The waves' clock: the sea's clock, the same on every client, run on between the calendar's steps at vanilla's pace,
/// as <c>DefaultShaderUniforms.Update</c> runs the foliage, so it only smooths.
/// </summary>
public sealed class WaveTime(SeaClock clock)
{
    private const double SnapSeconds = 1;
    private const double PullSeconds = 1;

    public double Seconds { get; private set; } = double.NaN;

    public double Advance(double deltaSeconds, double speedOfTime)
    {
        var world = clock.Seconds;
        var ahead = Seconds + deltaSeconds * speedOfTime / SeaClock.VanillaSpeedOfTime;
        Seconds = double.IsNaN(Seconds) || Math.Abs(world - ahead) > SnapSeconds
            ? world
            : ahead + (world - ahead) * (1 - Math.Exp(-deltaSeconds / PullSeconds));
        return Seconds;
    }
}
