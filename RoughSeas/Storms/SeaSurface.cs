using System;
using System.Linq;
using RoughSeas.SeaAndWeather;

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
    private readonly int headings;
    private readonly double[] amplitudeShare;
    private readonly double[] numberX, numberZ, frequency, amplitude;
    private readonly Trains?[] banks = new Trains?[2];
    private int active = -1;
    private double risingFrom = double.NegativeInfinity;

    public SeaSurface(SurfaceConfig config, Waves waves)
    {
        this.config = config;
        this.waves = waves;
        generation = config.Trains.Length;
        headings = (int)Math.Round(360 / config.TurnDegrees);
        var energy = config.Trains.Sum(train => train.Energy);
        amplitudeShare = config.Trains.Select(train => Math.Sqrt(train.Energy / energy) / (2 * Math.Sqrt(2))).ToArray();
        var slots = banks.Length * generation;
        numberX = new double[slots];
        numberZ = new double[slots];
        frequency = new double[slots];
        amplitude = new double[slots];
    }

    public int Slots => amplitude.Length;

    public double Seconds { get; private set; } = double.NaN;

    public long Frame { get; private set; }

    public double Steepness { get; private set; }

    public Foam Foam { get; private set; }

    /// <summary>The highest significant height around the player, which the surface is aimed at.</summary>
    public double SeaHeight { get; private set; }

    public double SurfDepth => config.SurfFoam ? waves.BreakingDepth(Math.Max(SeaHeight, SurfOnAnyShoreMetres)) : 0;

    /// <summary>The fold above which a crest whitens, set so the whitecaps cover the Beaufort force's share of the sea.</summary>
    public double CapsFold { get; private set; } = double.PositiveInfinity;

    public double TowardRadians { get; private set; }

    /// <summary>Where the rising trains run, fixed for their life, unlike <see cref="TowardRadians"/>, the wind at the last aim; <c>seafoam.fsh</c> lays its streaks along it.</summary>
    public double WavesTowardRadians => Rising is { } trains ? Toward(trains) : 0;

    public double DrawnHeight => 4 * Math.Sqrt(amplitude.Sum(value => value * value) / 2);

    public bool Calm { get; private set; } = true;

    public event Action<string>? Changed;

    public void Aim(double height, double towardRadians, double beaufort, double foamShare = 1)
    {
        Foam = Foam.Lerp(default, FoamAt(beaufort), Math.Clamp(foamShare, 0, 1));
        (SeaHeight, TowardRadians) = (height, towardRadians);
        CapsFold = FoldCovering(Math.Min(1, Foam.Caps / BreakingCrestShare));
    }

    public void Raise(WaveGeneration earlier, WaveGeneration latest, double bornSeconds, bool crossed = false)
    {
        var from = TrainsOf(earlier);
        if ((TrainsOf(latest) ?? from) is not { } next || Rising == next) return;

        if (Rising is { } drawn && drawn == from && !crossed) Rise(1 - active, next, bornSeconds);
        else if (active < 0 || !crossed)
        {
            if (from is { } first && first != next)
            {
                Load(0, first);
                Rise(1, next, bornSeconds);
            }
            else Rise(0, next, double.NegativeInfinity);
        }
        else
        {
            // The kept bank's weight carries on unbroken, and the new trains take the dropped bank's, so the drawn energy doesn't jump.
            var kept = banks[1 - active] == next || Handover >= 0.5 ? active : 1 - active;
            Rise(1 - kept, next, Seconds - Math.Acos(Weight(kept)) / (Math.PI / 2) * config.SettleSeconds);
        }
    }

    public void Advance(double seconds)
    {
        Seconds = seconds;
        Frame++;
        var handover = Handover;
        var (rising, fading) = (Math.Sin(handover * Math.PI / 2), Math.Cos(handover * Math.PI / 2));

        var slopes = 0.0;
        var calm = true;
        for (var slot = 0; slot < Slots; slot++)
        {
            amplitude[slot] = Wavenumber(slot) > 0 ? amplitudeShare[slot % generation] * SeaHeight * (slot / generation == active ? rising : fading) : 0;
            slopes += amplitude[slot] * Wavenumber(slot);
            calm &= amplitude[slot] <= Faded;
        }
        Calm = calm;
        Steepness = slopes > 0 ? Math.Min(config.Steepness, 1 / slopes) : config.Steepness;
    }

    /// <summary>The drawn surface's height here, scaled as the shader scales it by <see cref="SurfaceField.Factor"/>.</summary>
    public double HeightAt(double x, double z, double share = 1)
    {
        var (restX, restZ) = (x, z);
        for (var i = 0; i < SurfaceSearchSteps; i++)
        {
            var (shiftX, _, shiftZ) = DisplacementAt(restX, restZ);
            (restX, restZ) = (x - share * shiftX, z - share * shiftZ);
        }
        return share * DisplacementAt(restX, restZ).Y;
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

    private Trains? Rising => active >= 0 ? banks[active] : null;

    private double Handover =>Math.Clamp((Seconds - risingFrom) / config.SettleSeconds, 0, 1);

    private double Weight(int bank) => bank == active ? Math.Sin(Handover * Math.PI / 2) : Math.Cos(Handover * Math.PI / 2);

    private static void Squeeze(Span<double> into, double alongX, double alongZ, double pinch)
    {
        into[0] += alongX * alongX * pinch;
        into[1] += alongX * alongZ * pinch;
        into[2] += alongZ * alongZ * pinch;
    }

    private static double Folding(Span<double> squeeze) => 1 - (1 - squeeze[0]) * (1 - squeeze[2]) + squeeze[1] * squeeze[1];

    private double FoldCovering(double coverage)
    {
        if (coverage <= 0 || Calm || Rising is not { } trains) return double.PositiveInfinity;
        var spacing = Length(trains) / FoldSamplesAcross;
        var folds = new double[FoldSamplesAcross * FoldSamplesAcross];
        for (var i = 0; i < folds.Length; i++) folds[i] = FoldAt(i % FoldSamplesAcross * spacing * 3.1, i / FoldSamplesAcross * spacing * 2.7);
        Array.Sort(folds);
        return folds[(int)Math.Clamp((1 - coverage) * folds.Length, 0, folds.Length - 1)];
    }

    private double Phase(int slot, double x, double z) =>
        (numberX[slot] * x + numberZ[slot] * z - frequency[slot] * Seconds) % FullTurn;

    private double Wavenumber(int slot) => Math.Sqrt(numberX[slot] * numberX[slot] + numberZ[slot] * numberZ[slot]);

    private Trains? TrainsOf(WaveGeneration raised)
    {
        if (!raised.Raised) return null;
        var length = Gravity * raised.Period * raised.Period / FullTurn;
        var heading = (int)Math.Round(raised.TowardRadians / FullTurn * headings);
        return new Trains((heading % headings + headings) % headings, (int)Math.Round(Math.Log(length) / Math.Log(1 + config.Stretch)));
    }

    private double Toward(Trains trains) => trains.Heading * FullTurn / headings;

    private double Length(Trains trains) => Math.Pow(1 + config.Stretch, trains.Stretch);

    private void Rise(int bank, Trains trains, double from)
    {
        if (banks[bank] != trains) Load(bank, trains);
        (active, risingFrom) = (bank, from);
    }

    private void Load(int bank, Trains trains)
    {
        banks[bank] = trains;
        var (towardRadians, length) = (Toward(trains), Length(trains));
        for (var i = 0; i < generation; i++)
        {
            var train = config.Trains[i];
            var angle = towardRadians + train.AngleDegrees * Math.PI / 180;
            var number = FullTurn / Math.Max(config.MinLengthBlocks, length * train.LengthFactor);
            var slot = bank * generation + i;
            (numberX[slot], numberZ[slot], frequency[slot]) = (Math.Cos(angle) * number, Math.Sin(angle) * number, Math.Sqrt(Gravity * number));
            amplitude[slot] = 0;
        }
        Changed?.Invoke(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "new waves toward {0:F0}°, {1:F0} m long", (towardRadians * 180 / Math.PI + 360) % 360, length));
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

    private readonly record struct Trains(int Heading, int Stretch);
}

public readonly record struct Foam(double Caps, double Streaks, double Spray)
{
    public static Foam Of(WhitecapsConfig row) => new(row.Caps, row.Streaks, row.Spray);

    public static Foam Lerp(Foam from, Foam to, double share) => new(
        from.Caps + (to.Caps - from.Caps) * share,
        from.Streaks + (to.Streaks - from.Streaks) * share,
        from.Spray + (to.Spray - from.Spray) * share);
}
