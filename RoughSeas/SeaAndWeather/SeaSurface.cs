using System;
using System.Linq;

namespace RoughSeas.SeaAndWeather;

public class SeaSurface(SwellConfig config)
{
    private const double Gravity = 9.81;

    public double Period(double height) => config.PeriodSecondsPerRootMetre * Math.Sqrt(height);

    public SwellWave[] Waves(double height, double windX, double windZ)
    {
        var speed = Math.Sqrt(windX * windX + windZ * windZ);
        if (height <= 0 || speed <= 0) return [];

        var period = Period(height);
        var peakLength = Gravity * period * period / (2 * Math.PI);
        var toward = Math.Atan2(windZ, windX);
        return config.Components.Select(component =>
        {
            var angle = toward + component.AngleDegrees * Math.PI / 180;
            var number = 2 * Math.PI / (peakLength * component.LengthFactor);
            return new SwellWave(Math.Cos(angle) * number, Math.Sin(angle) * number, component.Share * height / 2, Math.Sqrt(Gravity * number));
        }).ToArray();
    }

    public double Steepness => config.Steepness;

    public SwellField Field() => new(this, config);
}

public readonly record struct SwellWave(double NumberX, double NumberZ, double Amplitude, double AngularFrequency);
