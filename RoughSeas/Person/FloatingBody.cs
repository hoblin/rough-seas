using System;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using RoughSeas.Water;

namespace RoughSeas.Person;

/// <summary>
/// One body in the water, stepped by its equation of motion; <see cref="BodyAfloat"/> only calls it. Up and down:
/// (m + m_a) dw/dt = ρ g V(η) - m g + (ρ V + m_a) a - ½ ρ Cd A |w - w_w| (w - w_w), with V what lies under the drawn
/// surface and a, w_w the water's own motion there, the stroke's lift with Jump on top. Across, as any
/// <see cref="BodyInWater"/>: the water's pressure and its push on a body moving through it, by the share
/// (ρ V + m_a) / (m + m_a) of what lies under, then the swimmer's stroke and the drag relative to the water and the
/// current, the roller of a breaker included. A crest broken over it churns air into the water it lies in.
/// </summary>
public sealed class FloatingBody(IPerson person, PersonConfig config, Waves waves) : BodyInWater
{
    private readonly Body body = person.Body;
    private readonly SwimmingArms arms = new(config.Body.Swimming, waves.DensityOf(false));
    private Breaker? breaker;

    /// <summary>What the last step found.</summary>
    public FloatReading Reading { get; private set; }

    public bool Breaking => breaker != null;

    /// <summary>A crest breaks over the body now; it takes over from any that broke before it, so successive crests renew the churn.</summary>
    public void Struck(IDrawnSea sea, BreakerPacket crest) =>
        breaker = new Breaker(sea.Seconds, crest.HeightMetres, crest.PeriodSeconds, crest.Celerity, crest.TowardRadians);

    /// <summary>The body's motion after this step, in blocks per tick, as vanilla's physics keeps it.</summary>
    /// <param name="wavesPerSecond">The waves' seconds to a second of <paramref name="seconds"/>: the water's motion, read on the waves' clock, is taken on the tick's, while the body answers in the tick's own seconds.</param>
    /// <param name="kept">The share of the motion across that vanilla's water drag keeps after this step; up and down it is undone.</param>
    public BodyMotion Step(IDrawnSea sea, WaterSurface surface, BodyPlace at, BodyMotion motion, Carried kit, double seconds, double wavesPerSecond, double kept,
        SwimStroke stroke = default, (double X, double Z) current = default)
    {
        var density = waves.DensityOf(surface.Salt);
        var waterline = surface.Height - at.Feet;
        var air = body.Squeezed(body.BreathingLitres, surface.Height - (at.Feet + body.ChestMetres), density, WaveTheory.Gravity);
        var under = body.LitresUnder(waterline, air);
        var whole = body.Litres(air);
        var massKg = body.MassKg + kit.Kg;
        var addedKg = body.AddedKg(waterline, air, density);
        var inertia = massKg + addedKg;
        var addedAcrossKg = stroke.Prone ? body.AddedAlongKg(waterline, air, density) : body.AddedAcrossKg(waterline, air, density);
        var inertiaAcross = massKg + addedAcrossKg;

        // the part of the body in a broken crest's water, which holds it up less
        var (churn, churned) = Churn(sea.Seconds, waterline, under, air);
        var held = (under + kit.Litres * under / whole) * (1 - churn * churned);

        var middle = Math.Max(0, surface.Height - (at.Feet + Math.Min(waterline, body.HeightMetres) / 2));
        var lift = Body.Lift(held, density, massKg, addedKg, WaveTheory.Gravity, sea.RiseAccelerationAt(at.X, at.Z, middle) * wavesPerSecond * wavesPerSecond);
        var rise = sea.RiseVelocityAt(at.X, at.Z, middle) * wavesPerSecond;
        var (toX, toZ) = Toward(sea.Seconds, churned, wavesPerSecond, current);
        var stroking = (ownX - toX) * stroke.X + (motion.Y * WaterDrag.TicksPerSecond - rise) * stroke.Y + (ownZ - toZ) * stroke.Z;
        var thrust = stroke.Stroking ? arms.Thrust(stroking, density) : 0;
        var heave = Body.Dragged(motion.Y * WaterDrag.TicksPerSecond - rise, density, config.Body.DragAreaAlong, inertia, seconds) + rise
            + (lift + thrust * stroke.Y / inertia) * seconds;

        var water = Water(sea, surface, at, waterline, wavesPerSecond, current);
        var share = (density * held / Body.LitresPerCubicMetre + addedAcrossKg) / inertiaAcross;
        var dragArea = stroke.Prone ? config.Body.DragAreaSwimming : config.Body.DragAreaAcross * under / whole;
        var before = Velocity;
        lock (gate)
        {
            Meet(water, (ownX - current.X, ownZ - current.Z), share, seconds);
            Drag((toX, toZ), (thrust * stroke.X / inertiaAcross, thrust * stroke.Z / inertiaAcross), density, dragArea, inertiaAcross, seconds);
        }
        var carried = Velocity;

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
        var inertiaAcross = body.MassKg + kit.Kg + body.AddedAcrossKg(waterline, air, density);
        var (churn, churned) = Churn(sea.Seconds, waterline, under, air);
        var held = (under + kit.Litres * under / whole) * (1 - churn * churned);
        var water = Water(sea, surface, at, waterline, wavesPerSecond, current);
        var share = (density * held / Body.LitresPerCubicMetre + body.AddedAcrossKg(waterline, air, density)) / inertiaAcross;
        lock (gate)
        {
            var (wavedX, wavedZ) = met ? (waveX, waveZ) : (water.WaveX, water.WaveZ);
            (ownX, ownZ) = (motion.X * WaterDrag.TicksPerSecond - wavedX, motion.Z * WaterDrag.TicksPerSecond - wavedZ);
            Meet(water, (ownX - current.X, ownZ - current.Z), share, seconds);
            Drag(Toward(sea.Seconds, churned, wavesPerSecond, current), default, density, config.Body.DragAreaAcross * under / whole, inertiaAcross, seconds);
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

    private void Drag((double X, double Z) to, (double X, double Z) thrust, double density, double dragArea, double inertia, double seconds)
    {
        var (awayX, awayZ) = (ownX + (Pushed.X + thrust.X) * seconds - to.X, ownZ + (Pushed.Z + thrust.Z) * seconds - to.Z);
        var away = Math.Sqrt(awayX * awayX + awayZ * awayZ);
        var kept = away > 0 ? Body.Dragged(away, density, dragArea, inertia, seconds) / away : 1;
        (ownX, ownZ) = (to.X + awayX * kept, to.Z + awayZ * kept);
    }
}

/// <summary>Where a body is: its feet in absolute block coordinates, the eye's height over them and its width, in metres.</summary>
public readonly record struct BodyPlace(double X, double Feet, double Z, double EyeHeight, double Width);

/// <summary>A body's motion, in blocks per tick, as vanilla's <c>Pos.Motion</c>.</summary>
public readonly record struct BodyMotion(double X, double Y, double Z);

/// <summary>What one step of a body found, for the log.</summary>
public readonly record struct FloatReading(double Waterline, double UnderLitres, double WholeLitres, double LungLitres, double Lift, double Churn,
    double FlowX, double Rise, double FlowZ, double CarriedX, double CarriedZ, double Thrust);
