using System;

namespace RoughSeas.Storms;

/// <summary>
/// The waves' clock: the world's game seconds at vanilla's normal pace, as <c>DefaultShaderUniforms.Update</c> runs the
/// foliage, so it is the same on every client; between the calendar's steps it only smooths.
/// </summary>
public sealed class WaveTime
{
    private const double VanillaSpeedOfTime = 60;
    private const double SnapSeconds = 1;
    private const double PullSeconds = 1;

    private double calendarSpeedMul = 0.5;

    public double Seconds { get; private set; } = double.NaN;

    public double Advance(double deltaSeconds, double speedOfTime, double totalHours, double calendarSpeedMul)
    {
        this.calendarSpeedMul = calendarSpeedMul;
        var world = SecondsAt(totalHours);
        var ahead = Seconds + deltaSeconds * speedOfTime / VanillaSpeedOfTime;
        Seconds = double.IsNaN(Seconds) || Math.Abs(world - ahead) > SnapSeconds
            ? world
            : ahead + (world - ahead) * (1 - Math.Exp(-deltaSeconds / PullSeconds));
        return Seconds;
    }

    public double SecondsAt(double totalHours) => totalHours * 3600 / (VanillaSpeedOfTime * calendarSpeedMul);

    public double HoursAt(double seconds) => seconds * VanillaSpeedOfTime * calendarSpeedMul / 3600;
}
