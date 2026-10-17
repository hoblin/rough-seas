using System;
using System.Linq;

namespace RoughSeas.Storms;

/// <summary>
/// The sea's surface as the client draws it: wave trains whose direction, length and speed stay fixed for their
/// life, advanced by one clock per frame. The water shader and every floating body read this one state.
/// <see cref="DisplacementAt"/> and <see cref="FoldAt"/> are the formulas of
/// <c>assets/roughseas/shaderincludes/seasurface.vsh</c>; a change to one is made to the other.
/// </summary>
public sealed class SeaSurface
{
    private const double Gravity = 9.81;
    private const double FullTurn = 2 * Math.PI;
    private const double Faded = 0.001;
    private const int SurfaceSearchSteps = 3;
    private const int FoldSamplesAcross = 24;

    private const double SurfOnAnyShoreMetres = 0.6;

    /// <summary>The foam trail looks back this far and twice as far, here and in the shader.</summary>
    public const double TrailSeconds = 0.7;

    /// <summary>The share of crests that <c>seafoam.fsh</c>'s breaking-crest mask lets break.</summary>
    private const double BreakingCrestShare = 0.3;

    private readonly SurfaceConfig config;
    private readonly Waves waves;
    private readonly int generation;
    private readonly double[] amplitudeShare;
    private readonly double[] numberX, numberZ, frequency, amplitude, target;
    private int active = -1;
    private double activeToward, activeLength;
    private Foam foamTarget;

    public SeaSurface(SurfaceConfig config, Waves waves)
    {
        this.config = config;
        this.waves = waves;
        generation = config.Trains.Length;
        var energy = config.Trains.Sum(train => train.Energy);
        amplitudeShare = config.Trains.Select(train => Math.Sqrt(train.Energy / energy) / (2 * Math.Sqrt(2))).ToArray();
        var slots = 2 * generation;
        numberX = new double[slots];
        numberZ = new double[slots];
        frequency = new double[slots];
        amplitude = new double[slots];
        target = new double[slots];
    }

    public int Slots => amplitude.Length;

    public double Seconds { get; private set; } = double.NaN;

    public long Frame { get; private set; }

    public double Steepness { get; private set; }

    public Foam Foam { get; private set; }

    /// <summary>The open sea's significant height the surface is aimed at.</summary>
    public double SeaHeight { get; private set; }

    public double SurfDepth => waves.BreakingDepth(Math.Max(SeaHeight, SurfOnAnyShoreMetres));

    /// <summary>The fold above which a crest whitens, set so the whitecaps cover the Beaufort force's share of the sea.</summary>
    public double CapsFold { get; private set; } = double.PositiveInfinity;

    public double TowardRadians { get; private set; }

    public double DrawnHeight => 4 * Math.Sqrt(amplitude.Sum(value => value * value) / 2);

    public bool Calm { get; private set; } = true;

    public event Action<string>? Changed;

    public static double OpenSea(double oceanity) => Math.Clamp((1 / Math.Max(0.2, 1 - oceanity) - 1) / 4, 0, 1);

    public void Aim(double height, double towardRadians, double beaufort)
    {
        foamTarget = FoamAt(beaufort);
        (SeaHeight, TowardRadians) = (height, towardRadians);
        var length = PeakLength(height);
        if (length > 0 && (active < 0 || !Alike(towardRadians, length)))
        {
            var next = active == 0 ? generation : 0;
            if (active < 0 || HasFaded(next)) Begin(next, towardRadians, length);
        }
        for (var slot = 0; slot < Slots; slot++)
        {
            target[slot] = active >= 0 && slot >= active && slot < active + generation ? amplitudeShare[slot - active] * height : 0;
        }
        CapsFold = FoldCovering(Math.Min(1, Foam.Caps / BreakingCrestShare));
    }

    public void Advance(double seconds)
    {
        if (seconds == Seconds) return;

        var blend = double.IsNaN(Seconds) ? 1 : 1 - Math.Exp(-(seconds - Seconds) / config.SettleSeconds);
        Seconds = seconds;
        Frame++;
        var slopes = 0.0;
        var calm = true;
        for (var slot = 0; slot < Slots; slot++)
        {
            amplitude[slot] += (target[slot] - amplitude[slot]) * blend;
            slopes += amplitude[slot] * Wavenumber(slot);
            calm &= amplitude[slot] <= Faded;
        }
        Calm = calm;
        Steepness = slopes > 0 ? Math.Min(config.Steepness, 1 / slopes) : config.Steepness;
        Foam = Foam.Lerp(Foam, foamTarget, blend);
    }

    public double HeightAt(double x, double z)
    {
        var (restX, restZ) = (x, z);
        for (var i = 0; i < SurfaceSearchSteps; i++)
        {
            var (shiftX, _, shiftZ) = DisplacementAt(restX, restZ);
            (restX, restZ) = (x - shiftX, z - shiftZ);
        }
        return DisplacementAt(restX, restZ).Y;
    }

    public (double X, double Y, double Z) DisplacementAt(double x, double z)
    {
        double shiftX = 0, rise = 0, shiftZ = 0;
        for (var slot = 0; slot < Slots; slot++)
        {
            if (amplitude[slot] <= 0) continue;
            var theta = Phase(slot, x, z);
            rise += amplitude[slot] * Math.Sin(theta);
            var sway = Steepness * amplitude[slot] * Math.Cos(theta) / Wavenumber(slot);
            shiftX += sway * numberX[slot];
            shiftZ += sway * numberZ[slot];
        }
        return (shiftX, rise, shiftZ);
    }

    public double FoldAt(double x, double z)
    {
        Span<double> now = stackalloc double[3], behind = stackalloc double[3], farBehind = stackalloc double[3];
        var steepest = 0.0;
        for (var slot = 0; slot < Slots; slot++)
        {
            if (amplitude[slot] <= 0) continue;
            var number = Wavenumber(slot);
            var (alongX, alongZ) = (numberX[slot] / number, numberZ[slot] / number);
            var pinch = Steepness * amplitude[slot] * number;
            var theta = Phase(slot, x, z);
            Squeeze(now, alongX, alongZ, pinch * Math.Sin(theta));
            Squeeze(behind, alongX, alongZ, pinch * Math.Sin(theta + frequency[slot] * TrailSeconds));
            Squeeze(farBehind, alongX, alongZ, pinch * Math.Sin(theta + frequency[slot] * 2 * TrailSeconds));
            steepest += pinch;
        }
        return steepest > 0 ? Math.Max(Folding(now), Math.Max(0.6 * Folding(behind), 0.35 * Folding(farBehind))) / steepest : 0;
    }

    public void WriteUniforms(double cameraX, double cameraZ, float[] into)
    {
        for (var slot = 0; slot < Slots; slot++)
        {
            into[slot * 4] = (float)numberX[slot];
            into[slot * 4 + 1] = (float)numberZ[slot];
            into[slot * 4 + 2] = (float)amplitude[slot];
            into[slot * 4 + 3] = (float)Phase(slot, cameraX, cameraZ);
        }
    }

    private static void Squeeze(Span<double> into, double alongX, double alongZ, double pinch)
    {
        into[0] += alongX * alongX * pinch;
        into[1] += alongX * alongZ * pinch;
        into[2] += alongZ * alongZ * pinch;
    }

    private static double Folding(Span<double> squeeze) => 1 - (1 - squeeze[0]) * (1 - squeeze[2]) + squeeze[1] * squeeze[1];

    private double FoldCovering(double coverage)
    {
        if (coverage <= 0 || Calm) return double.PositiveInfinity;
        var spacing = activeLength / FoldSamplesAcross;
        var folds = new double[FoldSamplesAcross * FoldSamplesAcross];
        for (var i = 0; i < folds.Length; i++) folds[i] = FoldAt(i % FoldSamplesAcross * spacing * 3.1, i / FoldSamplesAcross * spacing * 2.7);
        Array.Sort(folds);
        return folds[(int)Math.Clamp((1 - coverage) * folds.Length, 0, folds.Length - 1)];
    }

    private double Phase(int slot, double x, double z) =>
        (numberX[slot] * x + numberZ[slot] * z - frequency[slot] * Seconds) % FullTurn;

    private double Wavenumber(int slot) => Math.Sqrt(numberX[slot] * numberX[slot] + numberZ[slot] * numberZ[slot]);

    private double PeakLength(double height)
    {
        if (height <= 0) return 0;
        var period = waves.Period(height);
        return Gravity * period * period / FullTurn;
    }

    private bool Alike(double towardRadians, double length)
    {
        var turn = Math.Abs(Math.IEEERemainder(towardRadians - activeToward, FullTurn));
        return turn <= config.TurnDegrees * Math.PI / 180
            && Math.Abs(Math.Log(length / activeLength)) <= Math.Log(1 + config.Stretch);
    }

    private void Begin(int bank, double towardRadians, double length)
    {
        active = bank;
        (activeToward, activeLength) = (towardRadians, length);
        for (var i = 0; i < generation; i++)
        {
            var train = config.Trains[i];
            var angle = towardRadians + train.AngleDegrees * Math.PI / 180;
            var number = FullTurn / Math.Max(config.MinLengthBlocks, length * train.LengthFactor);
            var slot = bank + i;
            (numberX[slot], numberZ[slot], frequency[slot]) = (Math.Cos(angle) * number, Math.Sin(angle) * number, Math.Sqrt(Gravity * number));
            amplitude[slot] = 0;
        }
        Changed?.Invoke(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "new waves toward {0:F0}°, {1:F0} m long", (towardRadians * 180 / Math.PI + 360) % 360, length));
    }

    private bool HasFaded(int bank)
    {
        for (var slot = bank; slot < bank + generation; slot++)
        {
            if (amplitude[slot] > Faded) return false;
        }
        return true;
    }

    private Foam FoamAt(double beaufort)
    {
        var rows = config.Whitecaps;
        if (rows.Length == 0) return default;
        var above = Array.FindIndex(rows, row => row.Force >= beaufort);
        if (above < 0) return Foam.Of(rows[^1]);
        if (above == 0) return Foam.Of(rows[0]);
        var (low, high) = (rows[above - 1], rows[above]);
        return Foam.Lerp(Foam.Of(low), Foam.Of(high), (beaufort - low.Force) / (high.Force - low.Force));
    }
}

public readonly record struct Foam(double Caps, double Streaks, double Spray)
{
    public static Foam Of(WhitecapsConfig row) => new(row.Caps, row.Streaks, row.Spray);

    public static Foam Lerp(Foam from, Foam to, double share) => new(
        from.Caps + (to.Caps - from.Caps) * share,
        from.Streaks + (to.Streaks - from.Streaks) * share,
        from.Spray + (to.Spray - from.Spray) * share);
}
