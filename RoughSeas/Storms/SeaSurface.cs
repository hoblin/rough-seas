using System;
using System.Linq;
using RoughSeas.SeaAndWeather;
using RoughSeas.Water;

namespace RoughSeas.Storms;

/// <summary>
/// The sea's surface as the client draws it, and as the server builds it where a decision needs it (<see cref="SwellAround"/>):
/// the <see cref="WaveTrains"/> over the seabed. The water shader and every floating body read this one state.
/// <see cref="DisplacementAt"/> and <see cref="FoldAt"/> are the formulas of
/// <c>assets/roughseas/shaderincludes/seasurface.vsh</c>; a change to one is made to the other.
/// </summary>
public sealed class SeaSurface
{
    private const double FullTurn = 2 * Math.PI;
    private const int SurfaceSearchSteps = 3;
    private const int FoldSamplesAcross = 24;


    /// <summary>The foam trail looks back this far and twice as far, here and in the shader.</summary>
    public const double TrailSeconds = 0.7;

    /// <summary>The share of crests that <c>seafoam.fsh</c>'s breaking-crest mask lets break.</summary>
    private const double BreakingCrestShare = 0.3;

    private readonly SurfaceConfig config;
    private readonly Waves waves;
    private readonly object gate = new();
    private SurfField? seabed;

    public SeaSurface(SurfaceConfig config, Waves waves)
    {
        this.config = config;
        this.waves = waves;
        Trains = new WaveTrains(config);
    }

    public WaveTrains Trains { get; }

    public Waves Waves => waves;

    public Foam Foam { get; private set; }

    /// <summary>The fold above which a crest whitens, set so the whitecaps cover the Beaufort force's share of the sea.</summary>
    public double CapsFold { get; private set; } = double.PositiveInfinity;

    public SurfField? Seabed
    {
        get => seabed;
        set
        {
            lock (gate) seabed = value;
        }
    }

    public double BoreFrontSlope => config.Shore.BoreFrontSlope;

    public void Aim(double height, double beaufort, double foamShare = 1)
    {
        Foam = Foam.Lerp(default, FoamAt(beaufort), Math.Clamp(foamShare, 0, 1));
        Trains.Aim(height);
        CapsFold = FoldCovering(Math.Min(1, Foam.Caps / BreakingCrestShare));
    }

    public void Raise(WaveGeneration earlier, WaveGeneration latest, double bornSeconds, bool crossed = false)
    {
        lock (gate) Trains.Raise(earlier, latest, bornSeconds, crossed);
    }

    public void Advance(double seconds)
    {
        lock (gate) Trains.Advance(seconds);
    }

    /// <summary>The drawn surface's height here, scaled as the shader scales it by <see cref="SurfaceField.Factor"/>.</summary>
    public double HeightAt(double x, double z, double share = 1, bool shortWaves = true)
    {
        var (restX, restZ) = RestOf(x, z, share);
        return share * DisplacementAt(restX, restZ, share, shortWaves).Y;
    }

    /// <summary>
    /// The vertical velocity of the water drawn here, in metres a second of the waves' clock, <paramref name="below"/>
    /// metres under the drawn surface: the rate at which the drawn surface over that water rises, bores, reflections and
    /// short waves included, fading below it as <see cref="WaveTheory.RiseFade"/> says.
    /// </summary>
    public double RiseVelocityAt(double x, double z, double below = 0, double share = 1) => Rising(x, z, below, share, 1);

    /// <summary>The water's vertical acceleration where <see cref="RiseVelocityAt"/> reads its velocity, in metres a second squared of the waves' clock.</summary>
    public double RiseAccelerationAt(double x, double z, double below = 0, double share = 1) => Rising(x, z, below, share, 2);

    private double Rising(double x, double z, double below, double share, int order)
    {
        var (restX, restZ) = RestOf(x, z, share);
        var surface = share * DisplacementAt(restX, restZ, share).Y;
        return share * Displace(restX, restZ, share, true, new Motion(order, Math.Max(below, 0), surface)).Y;
    }

    private (double X, double Z) RestOf(double x, double z, double share)
    {
        var (restX, restZ) = (x, z);
        for (var i = 0; i < SurfaceSearchSteps; i++)
        {
            var (shiftX, _, shiftZ) = DisplacementAt(restX, restZ, share, false);
            (restX, restZ) = (x - share * shiftX, z - share * shiftZ);
        }
        return (restX, restZ);
    }

    /// <summary>The waves here, scaled as the shader scales them by <see cref="SurfaceField.Factor"/>, all of one update of the sea: the engine's ambient scan asks this off the main thread.</summary>
    public WaveReading WaveHere(double x, double z, double share)
    {
        lock (gate) return ReadWave(x, z, share);
    }

    private WaveReading ReadWave(double x, double z, double share)
    {
        var slots = Trains.Slots;
        Span<double> heights = stackalloc double[slots], lags = stackalloc double[slots], numbers = stackalloc double[slots];
        Span<double> towardX = stackalloc double[slots], towardZ = stackalloc double[slots];
        Local(x, z, share, heights, lags, numbers, towardX, towardZ);
        double squared = 0, deep = 0;
        for (var slot = 0; slot < slots; slot++)
        {
            squared += heights[slot] * heights[slot];
            deep += Trains.Amplitude(slot) * Trains.Amplitude(slot);
        }
        var dominant = Trains.Dominant;
        var covered = Seabed is { } field && field.Covers(x, z);
        var arriving = covered ? 1 + (Seabed!.ArrivingAt(x, z) - 1) * Seabed.EdgeShare(x, z) : 1;
        return new WaveReading(share * WaveTheory.SignificantHeight(squared), Trains.DominantPeriod, Math.Atan2(towardZ[dominant], towardX[dominant]),
            covered ? Seabed!.DepthAt(x, z) : double.PositiveInfinity, covered ? BedSlope(Seabed!, x, z, towardX[dominant], towardZ[dominant]) : 0, Broken(x, z, share), share * WaveTheory.SignificantHeight(deep) * arriving);
    }

    private double Broken(double x, double z, double share) =>
        share > 0 && Seabed is { } field && field.Covers(x, z) ? field.BreakingAt(x, z) * field.EdgeShare(x, z) : 0;

    /// <summary>How steeply the bed rises under a wave running this way.</summary>
    private static double BedSlope(SurfField field, double x, double z, double towardX, double towardZ) =>
        Math.Max(0, -(field.SlopeXAt(x, z) * towardX + field.SlopeZAt(x, z) * towardZ));

    private bool Plunges(double slope, double offshoreHeight) =>
        waves.PlungingBreaker(WaveTheory.Iribarren(slope, offshoreHeight, FullTurn / Trains.Wavenumber(Trains.Dominant)));

    /// <summary>The wave vector of a slot here, as the bed turns it.</summary>
    private (double X, double Z) Turned(SurfField field, int slot, double x, double z)
    {
        double Drawn(double atX, double atZ) => SurfField.Faded(field.LagAt(slot, atX, atZ), field.EdgeShare(atX, atZ)).Lag;
        double Across(double backX, double backZ, double aheadX, double aheadZ)
        {
            var (hasBack, hasAhead) = (!field.LandAt(backX, backZ), !field.LandAt(aheadX, aheadZ));
            var span = (hasBack ? 1 : 0) + (hasAhead ? 1 : 0);
            if (span == 0) return 0;
            var turn = Drawn(hasAhead ? aheadX : x, hasAhead ? aheadZ : z) - Drawn(hasBack ? backX : x, hasBack ? backZ : z);
            return (turn - FullTurn * Math.Round(turn / FullTurn)) / span;
        }
        return (Trains.NumberX(slot) + Across(x - 1, z, x + 1, z), Trains.NumberZ(slot) + Across(x, z - 1, x, z + 1));
    }

    private double Rise(double theta, double broken, double boreHeight, double number, int order = 0)
    {
        var sine = Math.Sin(theta + order * Math.PI / 2);
        if (broken <= 0) return sine;
        var front = WaveTheory.BoreFront(boreHeight, number, BoreFrontSlope);
        var bore = order switch { 0 => WaveTheory.Bore(theta, front), 1 => WaveTheory.BoreSlope(theta, front), _ => 0 };
        return sine + (bore - sine) * broken;
    }

    public (double X, double Y, double Z) DisplacementAt(double x, double z, double share = 0, bool shortWaves = true) =>
        Displace(x, z, share, shortWaves, default);

    private (double X, double Y, double Z) Displace(double x, double z, double share, bool shortWaves, Motion motion)
    {
        var slots = Trains.Slots;
        Span<double> heights = stackalloc double[slots], lags = stackalloc double[slots], numbers = stackalloc double[slots];
        Span<double> towardX = stackalloc double[slots], towardZ = stackalloc double[slots];
        var steepness = Local(x, z, share, heights, lags, numbers, towardX, towardZ);
        var facing = Facing(x, z, share);
        var limited = 1.0;
        if (facing is { } wall)
        {
            var total = 0.0;
            for (var slot = 0; slot < slots; slot++) total += heights[slot] * wall.Reflection * Math.Max(0, -(towardX[slot] * wall.NormalX + towardZ[slot] * wall.NormalZ));
            var allowed = Seabed!.FaceSlope * Math.Max(0, wall.Distance - FaceReading.FirstWaterVertex);
            if (total > allowed) limited = allowed / total;
        }
        double shiftX = 0, rise = 0, shiftZ = 0;
        var broken = Broken(x, z, share);
        var waterEnergy = 0.0;
        for (var slot = 0; slot < slots; slot++) waterEnergy += heights[slot] * heights[slot];
        var boreHeight = share * WaveTheory.SignificantHeight(waterEnergy);
        var bed = motion.Moving && share > 0 && Seabed is { } bedField && bedField.Covers(x, z) ? bedField.DepthAt(x, z) : double.PositiveInfinity;
        for (var slot = 0; slot < slots; slot++)
        {
            if (heights[slot] <= 0) continue;
            var theta = Trains.Phase(slot, x, z) + lags[slot];
            var rate = motion.Moving ? Math.Pow(-Trains.Frequency(slot), motion.Order) * WaveTheory.RiseFade(numbers[slot], bed, motion.Below, motion.Surface) : 1;
            rise += rate * heights[slot] * Rise(theta, broken, boreHeight, numbers[slot], motion.Order);
            var sway = (1 - broken) * steepness * heights[slot] * Math.Cos(theta);
            shiftX += sway * towardX[slot];
            shiftZ += sway * towardZ[slot];

            if (facing is not { } face) continue;
            var along = towardX[slot] * face.NormalX + towardZ[slot] * face.NormalZ;
            if (along >= 0) continue;
            var mirrored = theta - 2 * numbers[slot] * along * face.Distance;
            var returned = heights[slot] * face.Reflection * -along * limited;
            rise += rate * returned * Math.Sin(mirrored + motion.Order * Math.PI / 2);
            var back = steepness * returned * Math.Cos(mirrored);
            shiftX += back * (towardX[slot] - 2 * along * face.NormalX);
            shiftZ += back * (towardZ[slot] - 2 * along * face.NormalZ);
        }
        return (shiftX, shortWaves ? rise + ShortRise(x, z, share, motion, bed) : rise, shiftZ);
    }

    private readonly record struct Motion(int Order, double Below, double Surface)
    {
        public bool Moving => Order > 0 || Below > 0;
    }

    private double ShortRise(double x, double z, double share, Motion motion, double bed)
    {
        var kept = 1.0;
        if (share > 0 && Seabed is { } field && field.Covers(x, z))
        {
            if (field.LandAt(x, z)) return 0;
            if (Trains.ShortSeaHeight > 0) kept = Math.Min(1, Math.Max(field.DepthAt(x, z), 0) / (waves.BreakingDepthRatio * Trains.ShortSeaHeight));
        }
        return Trains.ShortRise(x, z, kept, motion.Order, motion.Below, bed, motion.Surface);
    }

    private FaceReading? Facing(double x, double z, double share) =>
        share > 0 && Seabed is { } field && field.Covers(x, z) && field.FaceAt(x, z) is { Reflection: > 0 } face ? face with { Reflection = face.Reflection * field.EdgeShare(x, z) } : null;

    /// <summary>The dominant train's crests here, as the bed holds them back: where they run, how fast, and how far apart.</summary>
    public (double TowardDegrees, double Speed, double Length) CrestAt(double x, double z)
    {
        var dominant = Trains.Dominant;
        var (alongX, alongZ) = Seabed is { } field && field.Covers(x, z) && Holds(field, dominant) ? Turned(field, dominant, x, z) : (Trains.NumberX(dominant), Trains.NumberZ(dominant));
        var number = Math.Sqrt(alongX * alongX + alongZ * alongZ);
        return number > 0 ? ((Math.Atan2(alongZ, alongX) * 180 / Math.PI + 360) % 360, Trains.Frequency(dominant) / number, FullTurn / number) : default;
    }

    /// <summary>How long ago, in seconds of the waves' clock, the dominant train's crest passed here.</summary>
    public double SinceCrest(double x, double z)
    {
        var dominant = Trains.Dominant;
        var turned = (Math.PI / 2 - Trains.Phase(dominant, x, z) - LagAt(dominant, x, z)) % FullTurn;
        return (turned < 0 ? turned + FullTurn : turned) / Trains.Frequency(dominant);
    }

    /// <summary>The cosine at which the dominant train, as the bed turns it here, runs into this outward normal; 0 for a train running away from it.</summary>
    public double Incidence(double x, double z, double normalX, double normalZ)
    {
        var dominant = Trains.Dominant;
        var (towardX, towardZ) = Seabed is { } field && field.Covers(x, z) && Holds(field, dominant) ? Turned(field, dominant, x, z) : (Trains.NumberX(dominant), Trains.NumberZ(dominant));
        var length = Math.Sqrt(towardX * towardX + towardZ * towardZ);
        return length > 0 ? Math.Max(0, -(towardX * normalX + towardZ * normalZ) / length) : 0;
    }

    /// <summary>The height of the wave passing here, trough to crest, over the bed, and whether a wave of that height breaks here.</summary>
    public (double Height, double Depth, bool Breaks) WaveAt(double x, double z, double share)
    {
        var slots = Trains.Slots;
        Span<double> heights = stackalloc double[slots], lags = stackalloc double[slots], numbers = stackalloc double[slots];
        Span<double> towardX = stackalloc double[slots], towardZ = stackalloc double[slots];
        Local(x, z, share, heights, lags, numbers, towardX, towardZ);
        double re = 0, im = 0;
        for (var slot = 0; slot < slots; slot++)
        {
            var theta = Trains.Phase(slot, x, z) + lags[slot];
            re += heights[slot] * Math.Cos(theta);
            im += heights[slot] * Math.Sin(theta);
        }
        var height = 2 * share * Math.Sqrt(re * re + im * im);
        return (height, Seabed?.DepthAt(x, z) ?? double.PositiveInfinity, WaveHere(x, z, share).Breaks(height));
    }

    /// <summary>The height of the wave whose sheet is on this land cell, as it stood <paramref name="secondsAgo"/> when its bore reached the shoreline, as <c>seasurface.vsh</c> reads it; and the height exceeded by 2% of the sea's waves.</summary>
    public (double Height, double TwoPercent) SheetWaveAt(double x, double z, double secondsAgo)
    {
        double re = 0, im = 0, energy = 0;
        for (var slot = 0; slot < Trains.Slots; slot++)
        {
            var theta = Trains.Phase(slot, x, z) + LagAt(slot, x, z) + Trains.Frequency(slot) * secondsAgo;
            var amplitude = Trains.Amplitude(slot);
            re += amplitude * Math.Cos(theta);
            im += amplitude * Math.Sin(theta);
            energy += amplitude * amplitude;
        }
        return (2 * Math.Sqrt(re * re + im * im), WaveTheory.TwoPercentRatio * WaveTheory.SignificantHeight(energy));
    }

    private bool Holds(SurfField field, int slot) => field.Holds(slot, Trains.NumberX(slot), Trains.NumberZ(slot));

    private double LagAt(int slot, double x, double z) =>
        Seabed is { } field && field.Covers(x, z) && Holds(field, slot) ? SurfField.Faded(field.LagAt(slot, x, z), field.EdgeShare(x, z)).Lag : 0;

    private double Local(double x, double z, double share, Span<double> heights, Span<double> lags, Span<double> numbers, Span<double> towardX,
        Span<double> towardZ)
    {
        var slots = Trains.Slots;
        for (var slot = 0; slot < slots; slot++)
        {
            var number = Trains.Wavenumber(slot);
            (heights[slot], lags[slot], numbers[slot]) = (Trains.Amplitude(slot), 0, number);
            (towardX[slot], towardZ[slot]) = number > 0 ? (Trains.NumberX(slot) / number, Trains.NumberZ(slot) / number) : (0, 0);
        }
        if (share <= 0 || Seabed is not { } field || !field.Covers(x, z)) return Trains.Steepness;

        if (field.LandAt(x, z))
        {
            heights.Clear();
            return Trains.Steepness;
        }

        var edge = field.EdgeShare(x, z);
        var offshore = 0.0;
        var slopes = 0.0;
        for (var slot = 0; slot < slots; slot++)
        {
            var deep = Trains.Wavenumber(slot);
            var valid = Holds(field, slot);
            var amplitude = Trains.Amplitude(slot);
            offshore += share * share * amplitude * amplitude;
            var (lag, kept) = valid ? SurfField.Faded(field.LagAt(slot, x, z), edge) : (0, 1);
            heights[slot] = amplitude * (valid ? 1 + (field.GainAt(slot, x, z) - 1) * edge : 1) * kept;
            numbers[slot] = deep > 0 ? deep + (WaveTheory.Wavenumber(deep, field.CelerityDepthAt(x, z)) - deep) * edge : 0;
            slopes += heights[slot] * numbers[slot];
            if (!valid) continue;
            lags[slot] = lag;
            var (turnedX, turnedZ) = Turned(field, slot, x, z);
            var turned = Math.Sqrt(turnedX * turnedX + turnedZ * turnedZ);
            if (turned > 0) (towardX[slot], towardZ[slot]) = (turnedX / turned, turnedZ / turned);
        }
        if (field.FaceAt(x, z) is { Reflection: > 0 } face)
        {
            for (var slot = 0; slot < slots; slot++)
            {
                slopes += heights[slot] * numbers[slot] * face.Reflection * edge * Math.Max(0, -(towardX[slot] * face.NormalX + towardZ[slot] * face.NormalZ));
            }
        }
        var cusp = slopes > 0 ? 1 / slopes : Trains.Steepness;
        var steepness = Math.Min(Trains.Steepness, cusp);
        return Plunges(BedSlope(field, x, z, towardX[Trains.Dominant], towardZ[Trains.Dominant]), WaveTheory.SignificantHeight(offshore)) ? steepness + (cusp - steepness) * field.BreakingAt(x, z) * edge : steepness;
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
        var slots = Trains.Slots;
        Span<double> speeds = stackalloc double[slots], phases = stackalloc double[slots], numbers = stackalloc double[slots];
        Span<double> towardX = stackalloc double[slots], towardZ = stackalloc double[slots];
        Orbital(x, z, lift, draft, yaw, length, beam, share, speeds, phases, numbers, towardX, towardZ);
        double velocityX = 0, velocityZ = 0;
        for (var slot = 0; slot < slots; slot++)
        {
            var speed = speeds[slot] * Math.Sin(phases[slot]);
            velocityX += speed * towardX[slot];
            velocityZ += speed * towardZ[slot];
        }
        return (velocityX, velocityZ);
    }

    /// <summary>
    /// How the water's velocity <see cref="HullVelocityAt"/> reads changes from one metre to the next over the same
    /// body, per second of the waves' clock: linear theory's ∂u_i/∂x_j, the same in both orders for waves.
    /// </summary>
    public FlowGradient HullGradientAt(double x, double z, double lift, double draft, double yaw, double length, double beam, double share = 1)
    {
        var slots = Trains.Slots;
        Span<double> speeds = stackalloc double[slots], phases = stackalloc double[slots], numbers = stackalloc double[slots];
        Span<double> towardX = stackalloc double[slots], towardZ = stackalloc double[slots];
        Orbital(x, z, lift, draft, yaw, length, beam, share, speeds, phases, numbers, towardX, towardZ);
        double xx = 0, xz = 0, zz = 0;
        for (var slot = 0; slot < slots; slot++)
        {
            var change = speeds[slot] * Math.Cos(phases[slot]) * numbers[slot];
            xx += change * towardX[slot] * towardX[slot];
            xz += change * towardX[slot] * towardZ[slot];
            zz += change * towardZ[slot] * towardZ[slot];
        }
        return new FlowGradient(xx, xz, zz);
    }

    private void Orbital(double x, double z, double lift, double draft, double yaw, double length, double beam, double share,
        Span<double> speeds, Span<double> phases, Span<double> numbers, Span<double> towardX, Span<double> towardZ)
    {
        var (sin, cos) = Math.SinCos(yaw + Math.PI / 2);
        var (alongX, alongZ) = (cos, -sin);
        Local(x, z, share, speeds, phases, numbers, towardX, towardZ);
        var depth = share > 0 && Seabed is { } field && field.Covers(x, z) ? field.CelerityDepthAt(x, z) : double.PositiveInfinity;
        for (var slot = 0; slot < Trains.Slots; slot++)
        {
            if (speeds[slot] <= 0)
            {
                speeds[slot] = 0;
                continue;
            }
            var along = numbers[slot] * (towardX[slot] * alongX + towardZ[slot] * alongZ);
            var across = numbers[slot] * (towardZ[slot] * alongX - towardX[slot] * alongZ);
            speeds[slot] *= share * Trains.Frequency(slot) * WaveTheory.OrbitalFade(numbers[slot], depth, lift, Math.Max(draft, 0))
                * Sinc(along * length / 2) * Sinc(across * beam / 2);
            phases[slot] += Trains.Phase(slot, x, z);
        }
    }

    private static double Sinc(double x) => Math.Abs(x) < 1e-9 ? 1 : Math.Sin(x) / x;

    public double FoldAt(double x, double z, double share = 0)
    {
        var slots = Trains.Slots;
        Span<double> heights = stackalloc double[slots], lags = stackalloc double[slots], numbers = stackalloc double[slots];
        Span<double> towardX = stackalloc double[slots], towardZ = stackalloc double[slots];
        var steepness = Local(x, z, share, heights, lags, numbers, towardX, towardZ);
        Span<double> now = stackalloc double[3], behind = stackalloc double[3], farBehind = stackalloc double[3];
        var broken = Broken(x, z, share);
        var waterEnergy = 0.0;
        for (var slot = 0; slot < slots; slot++) waterEnergy += heights[slot] * heights[slot];
        var boreHeight = share * WaveTheory.SignificantHeight(waterEnergy);
        var steepest = 0.0;
        for (var slot = 0; slot < slots; slot++)
        {
            if (heights[slot] <= 0) continue;
            var (alongX, alongZ) = (towardX[slot], towardZ[slot]);
            var pinch = steepness * heights[slot] * numbers[slot];
            var theta = Trains.Phase(slot, x, z) + lags[slot];
            Squeeze(now, alongX, alongZ, pinch * Rise(theta, broken, boreHeight, numbers[slot]));
            Squeeze(behind, alongX, alongZ, pinch * Math.Sin(theta + Trains.Frequency(slot) * TrailSeconds));
            Squeeze(farBehind, alongX, alongZ, pinch * Math.Sin(theta + Trains.Frequency(slot) * 2 * TrailSeconds));
            steepest += pinch;
        }
        return steepest > 0 ? Math.Max(Folding(now), Math.Max(0.6 * Folding(behind), 0.35 * Folding(farBehind))) / steepest : 0;
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
        if (coverage <= 0 || Trains.Calm || Trains.RisingLength is not { } length) return double.PositiveInfinity;
        var spacing = length / FoldSamplesAcross;
        var folds = new double[FoldSamplesAcross * FoldSamplesAcross];
        for (var i = 0; i < folds.Length; i++) folds[i] = FoldAt(i % FoldSamplesAcross * spacing * 3.1, i / FoldSamplesAcross * spacing * 2.7);
        Array.Sort(folds);
        return folds[(int)Math.Clamp((1 - coverage) * folds.Length, 0, folds.Length - 1)];
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
