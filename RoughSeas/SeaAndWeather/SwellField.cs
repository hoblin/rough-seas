using System;

namespace RoughSeas.SeaAndWeather;

public sealed class SwellField
{
    private const double FullTurn = 2 * Math.PI;
    private const int SurfaceSearchSteps = 3;

    private readonly SeaSurface surface;
    private readonly SwellConfig config;
    private readonly int generation;
    private readonly double[] numberX, numberZ, frequency, phase, amplitude, share;
    private SwellWave[] current = [];
    private int active = -1;
    private double height;
    private double anchorX, anchorZ;
    private double now = double.NaN;

    public SwellField(SeaSurface surface, SwellConfig config)
    {
        this.surface = surface;
        this.config = config;
        generation = config.Components.Length;
        var slots = 2 * generation;
        numberX = new double[slots];
        numberZ = new double[slots];
        frequency = new double[slots];
        phase = new double[slots];
        amplitude = new double[slots];
        share = new double[slots];
    }

    public int Slots => phase.Length;

    public void Aim(double waveHeight, double windX, double windZ)
    {
        height = waveHeight;
        var waves = surface.Waves(waveHeight, windX, windZ);
        if (waves.Length == 0 || (active >= 0 && Alike(current, waves))) return;

        active = active == 0 ? generation : 0;
        current = waves;
        for (var i = 0; i < generation; i++)
        {
            var slot = active + i;
            var wave = waves[i];
            (numberX[slot], numberZ[slot], frequency[slot]) = (wave.NumberX, wave.NumberZ, wave.AngularFrequency);
            (phase[slot], amplitude[slot], share[slot]) = (0, 0, config.Components[i].Share);
        }
    }

    public void Advance(double seconds)
    {
        if (seconds == now) return;

        var elapsed = double.IsNaN(now) ? 0 : seconds - now;
        var blend = double.IsNaN(now) ? 1 : 1 - Math.Exp(-elapsed / config.SettleSeconds);
        now = seconds;
        for (var slot = 0; slot < Slots; slot++)
        {
            var target = active >= 0 && slot >= active && slot < active + generation ? share[slot] * height / 2 : 0;
            amplitude[slot] += (target - amplitude[slot]) * blend;
            phase[slot] = (phase[slot] + frequency[slot] * elapsed) % FullTurn;
        }
    }

    public void Anchor(double x, double z)
    {
        for (var slot = 0; slot < Slots; slot++)
        {
            phase[slot] = (phase[slot] - numberX[slot] * (x - anchorX) - numberZ[slot] * (z - anchorZ)) % FullTurn;
        }
        anchorX = x;
        anchorZ = z;
    }

    public double DistanceFromAnchor(double x, double z) => Math.Sqrt((x - anchorX) * (x - anchorX) + (z - anchorZ) * (z - anchorZ));

    public bool Calm
    {
        get
        {
            foreach (var value in amplitude) if (value > 0) return false;
            return true;
        }
    }

    public double Height(double x, double z, double steepness)
    {
        var (restX, restZ) = (x, z);
        for (var i = 0; i < SurfaceSearchSteps; i++)
        {
            var (shiftX, _, shiftZ) = Displacement(restX, restZ, steepness);
            (restX, restZ) = (x - shiftX, z - shiftZ);
        }
        return Displacement(restX, restZ, steepness).Y;
    }

    public (double X, double Y, double Z) Displacement(double x, double z, double steepness)
    {
        double shiftX = 0, rise = 0, shiftZ = 0;
        for (var slot = 0; slot < Slots; slot++)
        {
            var number = Math.Sqrt(numberX[slot] * numberX[slot] + numberZ[slot] * numberZ[slot]);
            if (number == 0 || amplitude[slot] == 0) continue;

            var theta = numberX[slot] * (x - anchorX) + numberZ[slot] * (z - anchorZ) - phase[slot];
            rise += amplitude[slot] * Math.Sin(theta);
            var sway = steepness * amplitude[slot] * Math.Cos(theta) / number;
            shiftX += sway * numberX[slot];
            shiftZ += sway * numberZ[slot];
        }
        return (shiftX, rise, shiftZ);
    }

    public void ShaderWaves(double originX, double originZ, float[] into)
    {
        Array.Clear(into);
        for (var slot = 0; slot < Slots && slot * 4 < into.Length; slot++)
        {
            into[slot * 4] = (float)numberX[slot];
            into[slot * 4 + 1] = (float)numberZ[slot];
            into[slot * 4 + 2] = (float)amplitude[slot];
            into[slot * 4 + 3] = (float)((phase[slot] - numberX[slot] * (originX - anchorX) - numberZ[slot] * (originZ - anchorZ)) % FullTurn);
        }
    }

    private bool Alike(SwellWave[] before, SwellWave[] after)
    {
        var turn = Math.Abs(Math.Atan2(Cross(before[0], after[0]), Dot(before[0], after[0])));
        var stretch = Number(before[0]) / Number(after[0]);
        return turn <= config.TurnDegreesBeforeNewWaves * Math.PI / 180
            && Math.Abs(Math.Log(stretch)) <= Math.Log(1 + config.StretchBeforeNewWaves);
    }

    private static double Number(SwellWave wave) => Math.Sqrt(wave.NumberX * wave.NumberX + wave.NumberZ * wave.NumberZ);

    private static double Dot(SwellWave a, SwellWave b) => a.NumberX * b.NumberX + a.NumberZ * b.NumberZ;

    private static double Cross(SwellWave a, SwellWave b) => a.NumberX * b.NumberZ - a.NumberZ * b.NumberX;
}
