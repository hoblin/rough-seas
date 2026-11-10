using System;
using System.Linq;
using RoughSeas.SeaAndWeather;
using RoughSeas.Surf;

namespace RoughSeas.Storms;

/// <summary>
/// The sea's surface as the client draws it, and as the server builds it where a decision needs it (<see cref="SwellAround"/>):
/// wave trains whose direction, length and speed stay fixed for their life, advanced by one clock per frame. The water
/// shader and every floating body read this one state.
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
    private const double BreakingFromShare = 0.1;

    /// <summary>A sawtooth carries as much energy as a sine of 1 at this amplitude: its variance is a third of the square of it, a sine's a half.</summary>
    private const double BoreAmplitude = 1.2247449;

    /// <summary>The foam trail looks back this far and twice as far, here and in the shader.</summary>
    public const double TrailSeconds = 0.7;

    /// <summary>The share of crests that <c>seafoam.fsh</c>'s breaking-crest mask lets break.</summary>
    private const double BreakingCrestShare = 0.3;

    private readonly SurfaceConfig config;
    private readonly Waves waves;
    private readonly SurfShape shape;
    private readonly int generation;
    private readonly int headings;
    private readonly double[] amplitudeShare;
    private readonly double[] slotShares;
    private double shortSeaHeight;
    private readonly double[] numberX, numberZ, frequency, amplitude;
    private readonly (double Length, double Angle, double Amplitude)[] shortTrains;
    private readonly double shortHeight;
    private readonly double[] shortX, shortZ, shortFrequency, shortAmplitude;
    private readonly Trains?[] banks = new Trains?[2];
    private int active = -1;
    private double risingFrom = double.NegativeInfinity;

    public SeaSurface(SurfaceConfig config, Waves waves)
    {
        this.config = config;
        this.waves = waves;
        var (plungingFrom, plungingTo) = waves.PlungingIribarren;
        shape = new SurfShape(waves.BreakingDepthRatio, plungingFrom, plungingTo, waves.CrestRatio);
        generation = config.Trains.Length;
        headings = (int)Math.Round(360 / config.TurnDegrees);
        var energy = config.Trains.Sum(train => train.Energy);
        amplitudeShare = config.Trains.Select(train => Math.Sqrt(train.Energy / energy) / (2 * Math.Sqrt(2))).ToArray();
        var slots = banks.Length * generation;
        slotShares = Enumerable.Range(0, slots).Select(slot => amplitudeShare[slot % generation]).ToArray();
        numberX = new double[slots];
        numberZ = new double[slots];
        frequency = new double[slots];
        amplitude = new double[slots];

        var bands = config.ShortWaves.Bands.Select(band => (band, variance: config.ShortWaves.PhillipsConstant / (4 * FullTurn * FullTurn) * (band.ToBlocks * band.ToBlocks - band.FromBlocks * band.FromBlocks))).ToArray();
        shortTrains = bands.SelectMany(entry => entry.band.AnglesDegrees.Select(angle => (Math.Sqrt(entry.band.FromBlocks * entry.band.ToBlocks), angle * Math.PI / 180, Math.Sqrt(2 * entry.variance / entry.band.AnglesDegrees.Length)))).ToArray();
        shortHeight = 4 * Math.Sqrt(bands.Sum(entry => entry.variance));
        var shortSlots = banks.Length * shortTrains.Length;
        shortX = new double[shortSlots];
        shortZ = new double[shortSlots];
        shortFrequency = new double[shortSlots];
        shortAmplitude = new double[shortSlots];
    }

    public int Slots => amplitude.Length;

    public int ShortSlots => shortAmplitude.Length;

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

    public SurfField? Seabed { get; set; }

    public SurfShape Shape => shape;

    public (double X, double Z)[] Vectors() => Enumerable.Range(0, Slots).Select(slot => (numberX[slot], numberZ[slot])).ToArray();

    public double[] AmplitudeShares() => slotShares;

    public double BoreFrontSlope => config.Shore.BoreFrontSlope;

    private double Bore(double theta, double height, double number)
    {
        var front = Math.Clamp(height * number / (FullTurn * BoreFrontSlope), 0.02, 0.5);
        var ahead = ((theta - Math.PI / 2) % FullTurn + FullTurn) % FullTurn / FullTurn;
        return BoreAmplitude * (ahead < front ? 1 - 2 * ahead / front : 2 * (ahead - front) / (1 - front) - 1);
    }

    public int Dominant { get; private set; }

    public double DominantPeriod => frequency[Dominant] > 0 ? FullTurn / frequency[Dominant] : 0;

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
        var dominant = 0;
        for (var slot = 0; slot < Slots; slot++)
        {
            amplitude[slot] = Wavenumber(slot) > 0 ? amplitudeShare[slot % generation] * SeaHeight * (slot / generation == active ? rising : fading) : 0;
            slopes += amplitude[slot] * Wavenumber(slot);
            calm &= amplitude[slot] <= Faded;
            if (amplitude[slot] > amplitude[dominant]) dominant = slot;
        }
        var developed = shortHeight > 0 ? Math.Min(1, SeaHeight / shortHeight) : 0;
        for (var slot = 0; slot < ShortSlots; slot++)
        {
            var bank = slot / shortTrains.Length;
            var share = banks[0]?.Heading == banks[1]?.Heading ? (bank == active ? 1 : 0) : bank == active ? rising : fading;
            shortAmplitude[slot] = banks[bank] != null ? shortTrains[slot % shortTrains.Length].Amplitude * developed * share : 0;
        }
        shortSeaHeight = 4 * Math.Sqrt(shortAmplitude.Sum(value => value * value) / 2);
        Dominant = dominant;
        Calm = calm;
        Steepness = slopes > 0 ? Math.Min(config.Steepness, 1 / slopes) : config.Steepness;
    }

    /// <summary>The drawn surface's height here, scaled as the shader scales it by <see cref="SurfaceField.Factor"/>.</summary>
    public double HeightAt(double x, double z, double share = 1, bool shortWaves = true)
    {
        var (restX, restZ) = (x, z);
        for (var i = 0; i < SurfaceSearchSteps; i++)
        {
            var (shiftX, _, shiftZ) = DisplacementAt(restX, restZ, share, false);
            (restX, restZ) = (x - share * shiftX, z - share * shiftZ);
        }
        return share * DisplacementAt(restX, restZ, share, shortWaves).Y;
    }

    public (double X, double Y, double Z) DisplacementAt(double x, double z, double share = 0, bool shortWaves = true)
    {
        Span<double> heights = stackalloc double[Slots], lags = stackalloc double[Slots], numbers = stackalloc double[Slots];
        Span<double> towardX = stackalloc double[Slots], towardZ = stackalloc double[Slots];
        var steepness = Local(x, z, share, heights, lags, numbers, towardX, towardZ);
        var facing = Facing(x, z, share);
        var limited = 1.0;
        if (facing is { } wall)
        {
            var total = 0.0;
            for (var slot = 0; slot < Slots; slot++) total += heights[slot] * wall.Reflection * Math.Max(0, -(towardX[slot] * wall.NormalX + towardZ[slot] * wall.NormalZ));
            var allowed = Seabed!.FaceSlope * Math.Max(0, wall.Distance - FaceReading.FirstWaterVertex);
            if (total > allowed) limited = allowed / total;
        }
        double shiftX = 0, rise = 0, shiftZ = 0;
        var broken = share > 0 && Seabed is { } bed && bed.Covers(x, z) ? bed.BreakingAt(x, z) * bed.EdgeShare(x, z) : 0;
        var waterEnergy = 0.0;
        for (var slot = 0; slot < Slots; slot++) waterEnergy += heights[slot] * heights[slot];
        var boreHeight = share * 4 * Math.Sqrt(waterEnergy / 2);
        for (var slot = 0; slot < Slots; slot++)
        {
            if (heights[slot] <= 0) continue;
            var theta = Phase(slot, x, z) + lags[slot];
            rise += heights[slot] * (Math.Sin(theta) + (broken > 0 ? (Bore(theta, boreHeight, numbers[slot]) - Math.Sin(theta)) * broken : 0));
            var sway = (1 - broken) * steepness * heights[slot] * Math.Cos(theta);
            shiftX += sway * towardX[slot];
            shiftZ += sway * towardZ[slot];

            if (facing is not { } face) continue;
            var along = towardX[slot] * face.NormalX + towardZ[slot] * face.NormalZ;
            if (along >= 0) continue;
            var mirrored = theta - 2 * numbers[slot] * along * face.Distance;
            var returned = heights[slot] * face.Reflection * -along * limited;
            rise += returned * Math.Sin(mirrored);
            var back = steepness * returned * Math.Cos(mirrored);
            shiftX += back * (towardX[slot] - 2 * along * face.NormalX);
            shiftZ += back * (towardZ[slot] - 2 * along * face.NormalZ);
        }
        return (shiftX, shortWaves ? rise + ShortRise(x, z, share) : rise, shiftZ);
    }

    private double ShortRise(double x, double z, double share)
    {
        var kept = 1.0;
        if (share > 0 && Seabed is { } field && field.Covers(x, z))
        {
            var depth = field.DepthAt(x, z);
            if (depth <= 0) return 0;
            if (shortSeaHeight > 0) kept = Math.Min(1, depth / (shape.BreakingDepthRatio * shortSeaHeight));
        }
        var rise = 0.0;
        for (var slot = 0; slot < ShortSlots; slot++)
        {
            if (shortAmplitude[slot] <= 0) continue;
            rise += kept * shortAmplitude[slot] * Math.Sin(shortX[slot] * x + shortZ[slot] * z - shortFrequency[slot] * Seconds);
        }
        return rise;
    }

    private FaceReading? Facing(double x, double z, double share) =>
        share > 0 && Seabed is { } field && field.Covers(x, z) && field.FaceAt(x, z) is { Reflection: > 0 } face ? face with { Reflection = face.Reflection * field.EdgeShare(x, z) } : null;

    /// <summary>How long ago, in seconds of the waves' clock, the dominant train's crest passed here.</summary>
    public double SinceCrest(double x, double z)
    {
        var turned = (Math.PI / 2 - Phase(Dominant, x, z) - LagAt(Dominant, x, z)) % FullTurn;
        return (turned < 0 ? turned + FullTurn : turned) / frequency[Dominant];
    }

    /// <summary>The cosine at which the dominant train, as the bed turns it here, runs into this outward normal; 0 for a train running away from it.</summary>
    public double Incidence(double x, double z, double normalX, double normalZ)
    {
        var (towardX, towardZ) = (numberX[Dominant], numberZ[Dominant]);
        if (Seabed is { } field && field.Covers(x, z) && Dominant < field.Numbers.Length && Dominant < SurfField.Slots && field.Numbers[Dominant] == (towardX, towardZ))
        {
            towardX += (field.LagAt(Dominant, x + 1, z) - field.LagAt(Dominant, x - 1, z)) / 2;
            towardZ += (field.LagAt(Dominant, x, z + 1) - field.LagAt(Dominant, x, z - 1)) / 2;
        }
        var length = Math.Sqrt(towardX * towardX + towardZ * towardZ);
        return length > 0 ? Math.Max(0, -(towardX * normalX + towardZ * normalZ) / length) : 0;
    }

    /// <summary>The height of the wave passing here, trough to crest, over the bed, and whether the depth has capped this sea here, as the shader's <c>breaking</c> says.</summary>
    public (double Height, double Depth, bool Capped) WaveAt(double x, double z, double share)
    {
        Span<double> heights = stackalloc double[Slots], lags = stackalloc double[Slots], numbers = stackalloc double[Slots];
        Span<double> towardX = stackalloc double[Slots], towardZ = stackalloc double[Slots];
        Local(x, z, share, heights, lags, numbers, towardX, towardZ);
        double re = 0, im = 0, energy = 0;
        for (var slot = 0; slot < Slots; slot++)
        {
            var theta = Phase(slot, x, z) + lags[slot];
            re += heights[slot] * Math.Cos(theta);
            im += heights[slot] * Math.Sin(theta);
            energy += heights[slot] * heights[slot];
        }
        var depth = Seabed?.DepthAt(x, z) ?? double.PositiveInfinity;
        var breaking = Seabed is { } field && field.Covers(x, z) ? field.BreakingAt(x, z) * field.EdgeShare(x, z) : 0;
        return (2 * share * Math.Sqrt(re * re + im * im), depth, breaking >= BreakingFromShare);
    }

    /// <summary>The height of the wave whose sheet is on this land cell, and the height exceeded by 2% of the sea's waves.</summary>
    public (double Height, double TwoPercent) SheetWaveAt(double x, double z)
    {
        double re = 0, im = 0, energy = 0;
        for (var slot = 0; slot < Slots; slot++)
        {
            var theta = Phase(slot, x, z) + LagAt(slot, x, z);
            re += amplitude[slot] * Math.Cos(theta);
            im += amplitude[slot] * Math.Sin(theta);
            energy += amplitude[slot] * amplitude[slot];
        }
        return (2 * Math.Sqrt(re * re + im * im), Math.Sqrt(Math.Log(50) / 2) * 4 * Math.Sqrt(energy / 2));
    }

    private double LagAt(int slot, double x, double z) =>
        Seabed is { } field && field.Covers(x, z) && slot < field.Numbers.Length && slot < SurfField.Slots && field.Numbers[slot] == (numberX[slot], numberZ[slot])
            ? field.EdgeShare(x, z) * field.LagAt(slot, x, z)
            : 0;

    private double Local(double x, double z, double share, Span<double> heights, Span<double> lags, Span<double> numbers, Span<double> towardX,
        Span<double> towardZ)
    {
        for (var slot = 0; slot < Slots; slot++)
        {
            var number = Wavenumber(slot);
            (heights[slot], lags[slot], numbers[slot]) = (amplitude[slot], 0, number);
            (towardX[slot], towardZ[slot]) = number > 0 ? (numberX[slot] / number, numberZ[slot] / number) : (0, 0);
        }
        if (share <= 0 || Seabed is not { } field || !field.Covers(x, z)) return Steepness;

        var depth = field.DepthAt(x, z);
        if (depth <= 0)
        {
            heights.Clear();
            return Steepness;
        }

        var edge = field.EdgeShare(x, z);
        var offshore = 0.0;
        var slopes = 0.0;
        for (var slot = 0; slot < Slots; slot++)
        {
            var deep = Wavenumber(slot);
            var valid = slot < field.Numbers.Length && slot < SurfField.Slots && field.Numbers[slot] == (numberX[slot], numberZ[slot]);
            offshore += share * share * amplitude[slot] * amplitude[slot];
            heights[slot] = amplitude[slot] * (valid ? 1 + (field.GainAt(slot, x, z) - 1) * edge : 1);
            numbers[slot] = deep > 0 ? deep + (Shoaling.Wavenumber(deep, depth) - deep) * edge : 0;
            slopes += heights[slot] * numbers[slot];
            if (!valid) continue;
            lags[slot] = edge * field.LagAt(slot, x, z);
            var (turnedX, turnedZ) = (numberX[slot] + edge * (field.LagAt(slot, x + 1, z) - field.LagAt(slot, x - 1, z)) / 2,
                numberZ[slot] + edge * (field.LagAt(slot, x, z + 1) - field.LagAt(slot, x, z - 1)) / 2);
            var turned = Math.Sqrt(turnedX * turnedX + turnedZ * turnedZ);
            if (turned > 0) (towardX[slot], towardZ[slot]) = (turnedX / turned, turnedZ / turned);
        }
        if (field.FaceAt(x, z) is { Reflection: > 0 } face)
        {
            for (var slot = 0; slot < Slots; slot++)
            {
                slopes += heights[slot] * numbers[slot] * face.Reflection * edge * Math.Max(0, -(towardX[slot] * face.NormalX + towardZ[slot] * face.NormalZ));
            }
        }
        var cusp = slopes > 0 ? 1 / slopes : Steepness;
        var steepness = Math.Min(Steepness, cusp);
        var slope = Math.Sqrt(Math.Pow(field.SlopeXAt(x, z), 2) + Math.Pow(field.SlopeZAt(x, z), 2));
        return shape.Plunges(slope, 4 * Math.Sqrt(offshore / 2), Wavenumber(Dominant)) ? steepness + (cusp - steepness) * field.BreakingAt(x, z) * edge : steepness;
    }

    /// <summary>
    /// The water's horizontal velocity, in metres a second, averaged over the body of a hull centred here that floats
    /// <paramref name="lift"/> metres over still water and reaches <paramref name="draft"/> metres under it, with its
    /// <paramref name="length"/> along <paramref name="yaw"/> as <see cref="DrawnSea.PoseOf"/> takes it and its
    /// <paramref name="beam"/> across. The water's push on a hull is its pressure summed over the hull (the
    /// Froude–Krylov force), so it is linear wave theory's orbital velocity averaged over that body: e^(k z) over the draft,
    /// e^(k lift) (1 − e^(−k draft)) / (k draft), and sinc(k_along length / 2) sinc(k_across beam / 2) over the plan.
    /// </summary>
    public (double X, double Z) HullVelocityAt(double x, double z, double lift, double draft, double yaw, double length, double beam,
        double share = 1)
    {
        var (sin, cos) = Math.SinCos(yaw + Math.PI / 2);
        return Orbital(x, z, lift, Math.Max(draft, 0), cos, -sin, length, beam, share);
    }

    private (double X, double Z) Orbital(double x, double z, double height, double draft, double alongX, double alongZ,
        double length, double beam, double share)
    {
        Span<double> heights = stackalloc double[Slots], lags = stackalloc double[Slots], numbers = stackalloc double[Slots];
        Span<double> towardX = stackalloc double[Slots], towardZ = stackalloc double[Slots];
        Local(x, z, share, heights, lags, numbers, towardX, towardZ);
        double velocityX = 0, velocityZ = 0;
        for (var slot = 0; slot < Slots; slot++)
        {
            if (heights[slot] <= 0) continue;
            var number = Wavenumber(slot);
            var along = numbers[slot] * (towardX[slot] * alongX + towardZ[slot] * alongZ);
            var across = numbers[slot] * (towardZ[slot] * alongX - towardX[slot] * alongZ);
            var fade = Math.Exp(number * height) * (draft > 0 ? (1 - Math.Exp(-number * draft)) / (number * draft) : 1);
            var speed = share * heights[slot] * frequency[slot] * Math.Sin(Phase(slot, x, z) + lags[slot]) * fade
                * Sinc(along * length / 2) * Sinc(across * beam / 2);
            velocityX += speed * towardX[slot];
            velocityZ += speed * towardZ[slot];
        }
        return (velocityX, velocityZ);
    }

    private static double Sinc(double x) => Math.Abs(x) < 1e-9 ? 1 : Math.Sin(x) / x;

    public double FoldAt(double x, double z, double share = 0)
    {
        Span<double> heights = stackalloc double[Slots], lags = stackalloc double[Slots], numbers = stackalloc double[Slots];
        Span<double> towardX = stackalloc double[Slots], towardZ = stackalloc double[Slots];
        var steepness = Local(x, z, share, heights, lags, numbers, towardX, towardZ);
        Span<double> now = stackalloc double[3], behind = stackalloc double[3], farBehind = stackalloc double[3];
        var steepest = 0.0;
        for (var slot = 0; slot < Slots; slot++)
        {
            if (heights[slot] <= 0) continue;
            var number = Wavenumber(slot);
            var (alongX, alongZ) = (towardX[slot], towardZ[slot]);
            var pinch = steepness * heights[slot] * numbers[slot];
            var theta = Phase(slot, x, z) + lags[slot];
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

    public void WriteShortUniforms(double cameraX, double cameraZ, float[] into)
    {
        for (var slot = 0; slot < ShortSlots; slot++)
        {
            into[slot * 4] = (float)shortX[slot];
            into[slot * 4 + 1] = (float)shortZ[slot];
            into[slot * 4 + 2] = (float)shortAmplitude[slot];
            into[slot * 4 + 3] = (float)((shortX[slot] * cameraX + shortZ[slot] * cameraZ - shortFrequency[slot] * Seconds) % FullTurn);
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
        for (var i = 0; i < shortTrains.Length; i++)
        {
            var angle = towardRadians + shortTrains[i].Angle;
            var number = FullTurn / shortTrains[i].Length;
            var slot = bank * shortTrains.Length + i;
            (shortX[slot], shortZ[slot], shortFrequency[slot]) = (Math.Cos(angle) * number, Math.Sin(angle) * number, Math.Sqrt(Gravity * number));
            shortAmplitude[slot] = 0;
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
