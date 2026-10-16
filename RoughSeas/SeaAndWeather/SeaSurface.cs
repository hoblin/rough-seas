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

    public static double Height(SwellWave[] waves, double seconds, double x, double z)
    {
        double height = 0;
        foreach (var wave in waves)
        {
            height += wave.Amplitude * Math.Sin(wave.NumberX * x + wave.NumberZ * z - wave.AngularFrequency * seconds);
        }
        return height;
    }

    public static void ShaderWaves(SwellWave[] waves, double seconds, double originX, double originZ, float[] into)
    {
        Array.Clear(into);
        for (var i = 0; i < waves.Length && i * 4 < into.Length; i++)
        {
            var wave = waves[i];
            into[i * 4] = (float)wave.NumberX;
            into[i * 4 + 1] = (float)wave.NumberZ;
            into[i * 4 + 2] = (float)wave.Amplitude;
            into[i * 4 + 3] = (float)((wave.AngularFrequency * seconds - wave.NumberX * originX - wave.NumberZ * originZ) % (2 * Math.PI));
        }
    }
}

public readonly record struct SwellWave(double NumberX, double NumberZ, double Amplitude, double AngularFrequency);
