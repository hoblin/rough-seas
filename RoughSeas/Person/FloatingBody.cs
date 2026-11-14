using System;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using RoughSeas.Water;

namespace RoughSeas.Person;

/// <summary>
/// One body in the water, stepped by its equation of motion; <see cref="BodyAfloat"/> only calls it. Up and down:
/// (m + m_a) dw/dt = ρ g V(η) - m g + (ρ V + m_a) a - D, with V what lies under the drawn surface and a the water's own
/// motion there, the stroke's lift with Jump on top. Across, as any <see cref="BodyInWater"/>: the water's pressure and
/// its push on a body moving through it, by the share (ρ V + m_a) / (m + m_a) of what lies under, then the swimmer's
/// stroke. The drag D takes the body's velocity through the water, the current and a breaker's roller included, along
/// and across the body as it lies (<see cref="Posture"/>). A crest broken over it churns air into the water it lies in.
/// A body that comes into the water brings its way in as its own.
/// </summary>
public sealed class FloatingBody(IPerson person, PersonConfig config, Waves waves) : BodyInWater
{
    private readonly Body body = person.Body;
    private readonly SwimmingArms arms = new(config.Body.Swimming, waves.DensityOf(false));
    private Breaker? breaker;
    private Posture lying = Posture.Upright;

    /// <summary>What the last step found.</summary>
    public FloatReading Reading { get; private set; }

    public bool Breaking => breaker != null;

    /// <summary>A crest breaks over the body now; it takes over from any that broke before it, so successive crests renew the churn.</summary>
    public void Struck(IDrawnSea sea, BreakerPacket crest) =>
        breaker = new Breaker(sea.Seconds, crest.HeightMetres, crest.PeriodSeconds, crest.Celerity, crest.TowardRadians);

    /// <summary>The body's motion after this step, in blocks per tick, as vanilla's physics keeps it.</summary>
    /// <param name="wavesPerSecond">The waves' seconds to a second of <paramref name="seconds"/>: the water's motion, read on the waves' clock, is taken on the tick's, while the body answers in the tick's own seconds.</param>
    /// <param name="kept">The share of the motion across that vanilla's water drag keeps after this step; up and down it is undone.</param>
    /// <param name="drawn">How the body is drawn lying, which it turns toward at its pace; upright when not given.</param>
    public BodyMotion Step(IDrawnSea sea, WaterSurface surface, BodyPlace at, BodyMotion motion, Carried kit, double seconds, double wavesPerSecond, double kept,
        SwimStroke stroke = default, (double X, double Z) current = default, Posture? drawn = null)
    {
        var density = waves.DensityOf(surface.Salt);
        var waterline = surface.Height - at.Feet;
        var air = body.Squeezed(body.BreathingLitres, surface.Height - (at.Feet + body.ChestMetres), density, WaveTheory.Gravity);
        var under = body.LitresUnder(waterline, air);
        var whole = body.Litres(air);
        var posture = lying = lying.TurnedToward(drawn ?? Posture.Upright, config.Body.TurnRadiansPerSecond * seconds);
        var massKg = body.MassKg + kit.Kg;
        var addedKg = body.AddedKg(waterline, air, density);
        var inertia = massKg + addedKg;
        var addedAcrossKg = AddedAcrossKg(posture, waterline, air, density);
        var inertiaAcross = massKg + addedAcrossKg;

        // the part of the body in a broken crest's water, which holds it up less
        var (churn, churned) = Churn(sea.Seconds, waterline, under, air);
        var held = (under + kit.Litres * under / whole) * (1 - churn * churned);

        var middle = Math.Max(0, surface.Height - (at.Feet + Math.Min(waterline, body.HeightMetres) / 2));
        var lift = Body.Lift(held, density, massKg, addedKg, WaveTheory.Gravity, sea.RiseAccelerationAt(at.X, at.Z, middle) * wavesPerSecond * wavesPerSecond);
        var rise = sea.RiseVelocityAt(at.X, at.Z, middle) * wavesPerSecond;
        var water = Water(sea, surface, at, waterline, wavesPerSecond, current);
        lock (gate)
        {
            if (!met) TakeAsOwn(motion, water);
        }
        var (toX, toZ) = Toward(sea.Seconds, churned, wavesPerSecond, current);
        var stroking = (ownX - toX) * stroke.X + (motion.Y * WaterDrag.TicksPerSecond - rise) * stroke.Y + (ownZ - toZ) * stroke.Z;
        var thrust = stroke.Stroking ? arms.Thrust(stroking, density) : 0;

        var share = (density * held / Body.LitresPerCubicMetre + addedAcrossKg) / inertiaAcross;
        var before = Velocity;
        double heaveThrough;
        lock (gate)
        {
            Meet(water, (ownX - current.X, ownZ - current.Z), share, seconds);
            heaveThrough = Drag((toX, toZ), (thrust * stroke.X / inertiaAcross, thrust * stroke.Z / inertiaAcross), motion.Y * WaterDrag.TicksPerSecond - rise,
                density, posture, under / whole, Inertias(posture, massKg, waterline, air, density), seconds);
        }
        var carried = Velocity;
        var heave = heaveThrough + rise + (lift + thrust * stroke.Y / inertia) * seconds;

        Reading = new FloatReading(waterline, under, whole, air, lift, churn * churned, water.WaveX, rise, water.WaveZ, carried.X, carried.Z, thrust);
        return new BodyMotion(
            motion.X + WaterDrag.Carried(carried.X, before.X, 1, kept),
            heave / WaterDrag.TicksPerSecond,
            motion.Z + WaterDrag.Carried(carried.Z, before.Z, 1, kept));
    }

    /// <summary>
    /// A wading body's motion across after this step, in blocks per tick: vanilla's walk, jump and gravity move it, and in
    /// place of vanilla's water drag, the water meets the part of it under the surface as it meets a swimmer, its drag
    /// that part's. <paramref name="motion"/> is the motion before vanilla's drag, which is undone across.
    /// </summary>
    public (double X, double Z) Wade(IDrawnSea sea, WaterSurface surface, BodyPlace at, BodyMotion motion, Carried kit, double seconds, double wavesPerSecond,
        (double X, double Z) current = default)
    {
        var density = waves.DensityOf(surface.Salt);
        var waterline = surface.Height - at.Feet;
        var air = body.BreathingLitres;
        var under = body.LitresUnder(waterline, air);
        var whole = body.Litres(air);
        var massKg = body.MassKg + kit.Kg;
        var addedAcrossKg = body.AddedAcrossKg(waterline, air, density);
        var inertiaAcross = massKg + addedAcrossKg;
        var (churn, churned) = Churn(sea.Seconds, waterline, under, air);
        var held = (under + kit.Litres * under / whole) * (1 - churn * churned);
        var water = Water(sea, surface, at, waterline, wavesPerSecond, current);
        var share = (density * held / Body.LitresPerCubicMetre + addedAcrossKg) / inertiaAcross;
        lying = Posture.Upright;
        lock (gate)
        {
            TakeAsOwn(motion, water);
            Meet(water, (ownX - current.X, ownZ - current.Z), share, seconds);
            Drag(Toward(sea.Seconds, churned, wavesPerSecond, current), default, 0, density, lying, under / whole,
                Inertias(lying, massKg, waterline, air, density), seconds);
        }
        var carried = Velocity;

        Reading = new FloatReading(waterline, under, whole, air, 0, churn * churned, water.WaveX, 0, water.WaveZ, carried.X, carried.Z, 0);
        return (carried.X / WaterDrag.TicksPerSecond, carried.Z / WaterDrag.TicksPerSecond);
    }

    private (double Churn, double Churned) Churn(double now, double waterline, double under, double air)
    {
        if (breaker is not { } crest) return (0, 0);
        if (crest.Spent(config.Breaking, now - crest.BrokeSeconds))
        {
            breaker = null;
            return (0, 0);
        }
        var below = under - body.LitresUnder(waterline - crest.PlumeDepth(config.Breaking), air);
        return (crest.AirShare(config.Breaking, now - crest.BrokeSeconds), under > 0 ? below / under : 0);
    }

    private (double X, double Z) Toward(double now, double churned, double wavesPerSecond, (double X, double Z) current)
    {
        if (breaker is not { } roller) return current;
        var speed = churned * roller.RollerSpeed(config.Breaking, now - roller.BrokeSeconds) * wavesPerSecond;
        return (current.X + speed * Math.Cos(roller.TowardRadians), current.Z + speed * Math.Sin(roller.TowardRadians));
    }

    private WaterOnHull Water(IDrawnSea sea, WaterSurface surface, BodyPlace at, double waterline, double wavesPerSecond, (double X, double Z) current)
    {
        var top = surface.Swell - Math.Max(0, waterline - body.HeightMetres);
        var depth = Math.Min(waterline, body.HeightMetres);
        var flow = sea.HullVelocityAt(at.X, at.Z, top, depth, 0, at.Width, at.Width);
        var gradient = sea.HullGradientAt(at.X, at.Z, top, depth, 0, at.Width, at.Width).Scaled(wavesPerSecond);
        return new WaterOnHull(current.X, current.Z, flow.X * wavesPerSecond, flow.Z * wavesPerSecond, gradient);
    }

    private double AddedAcrossKg(Posture posture, double waterline, double air, double density) =>
        posture.Prone ? body.AddedAlongKg(waterline, air, density) : body.AddedAcrossKg(waterline, air, density);

    private void TakeAsOwn(BodyMotion motion, WaterOnHull water)
    {
        if (!met) (waveX, waveZ) = (water.WaveX, water.WaveZ);
        (ownX, ownZ) = (motion.X * WaterDrag.TicksPerSecond - waveX, motion.Z * WaterDrag.TicksPerSecond - waveZ);
    }

    private (double Along, double Across) Inertias(Posture posture, double massKg, double waterline, double air, double density) =>
        (massKg + (posture.Prone ? body.AddedAlongKg(waterline, air, density) : body.AddedKg(waterline, air, density)),
            massKg + body.AddedAcrossKg(waterline, air, density));

    private double Drag((double X, double Z) to, (double X, double Z) thrust, double heaveThrough, double density, Posture posture, double shareUnder,
        (double Along, double Across) inertias, double seconds)
    {
        var (awayX, awayZ) = (ownX + (Pushed.X + thrust.X) * seconds - to.X, ownZ + (Pushed.Z + thrust.Z) * seconds - to.Z);
        var (x, y, z) = posture.Dragged((awayX, heaveThrough, awayZ), config.Body, density, shareUnder, inertias.Along, inertias.Across, seconds);
        (ownX, ownZ) = (to.X + x, to.Z + z);
        return y;
    }
}

/// <summary>Where a body is: its feet in absolute block coordinates, the eye's height over them and its width, in metres.</summary>
public readonly record struct BodyPlace(double X, double Feet, double Z, double EyeHeight, double Width);

/// <summary>A body's motion, in blocks per tick, as vanilla's <c>Pos.Motion</c>.</summary>
public readonly record struct BodyMotion(double X, double Y, double Z);

/// <summary>What one step of a body found, for the log.</summary>
public readonly record struct FloatReading(double Waterline, double UnderLitres, double WholeLitres, double LungLitres, double Lift, double Churn,
    double FlowX, double Rise, double FlowZ, double CarriedX, double CarriedZ, double Thrust);
