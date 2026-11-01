using System;

namespace RoughSeas.Vessels;

/// <summary>
/// The waves a hull makes, by Michell's thin-ship integral (Michell 1898; Tuck 1989) over a Wigley hull of her length,
/// beam and draft: parabolic along her and down her. Newtons against her at a speed in m/s.
/// </summary>
public sealed class WaveMaking(double lengthMetres, double beamMetres, double draftMetres, double waterDensity)
{
    private const double Gravity = 9.81;
    private const double SlowestMetresPerSecond = 0.05;
    private const double DeepestDecay = 30;
    private const int Steps = 240;

    public double Newtons(double metresPerSecond)
    {
        var speed = Math.Abs(metresPerSecond);
        if (speed < SlowestMetresPerSecond || draftMetres <= 0) return 0;

        var waveNumber = Gravity / (speed * speed);
        var widest = Math.Acosh(Math.Max(1.000001, Math.Sqrt(DeepestDecay / (waveNumber * draftMetres))));
        var step = widest / Steps;
        var sum = 0.0;
        for (var i = 0; i <= Steps; i++)
        {
            var u = i * step;
            var lambda = Math.Cosh(u);
            var weight = i == 0 || i == Steps ? 1 : i % 2 == 1 ? 4 : 2;
            sum += weight * Amplitude(lambda, waveNumber) * lambda * lambda;
        }
        return 4 * waterDensity * Gravity * Gravity / (Math.PI * speed * speed) * sum * step / 3;
    }

    private double Amplitude(double lambda, double waveNumber)
    {
        var along = lambda * waveNumber;
        var half = lengthMetres / 2;
        var lengthwise = 2 * (Math.Sin(along * half) / (along * along) - half * Math.Cos(along * half) / along);
        var decay = lambda * lambda * waveNumber;
        var t = draftMetres;
        var fade = Math.Exp(-decay * t);
        var downward = (1 - fade) / decay - (2 - fade * (decay * decay * t * t + 2 * decay * t + 2)) / (decay * decay * decay * t * t);
        var slope = 4 * beamMetres / (lengthMetres * lengthMetres);
        var amplitude = slope * lengthwise * downward;
        return amplitude * amplitude;
    }
}
