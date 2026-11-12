using System;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using RoughSeas.Water;

namespace RoughSeas.Person;

/// <summary>
/// One body in the water, stepped by its equation of motion; <see cref="BodyAfloat"/> only calls it. Up and down:
/// (m + m_a) dw/dt = ρ g V(η) - m g + (ρ V + m_a) a - ½ ρ Cd A |w - w_w| (w - w_w), with V what lies under the drawn
/// surface and a, w_w the water's own motion there. Across the same form: the water's pressure, its push on a body moving
/// through it, the swimmer's stroke and the drag relative to the water, the roller of a breaker included. A crest broken
/// over it churns air into the water it lies in.
/// </summary>
public sealed class FloatingBody(IPerson person, PersonConfig config, Waves waves)
{
    private readonly Body body = person.Body;
    private (double X, double Z) carried, water;
    private bool met;
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
        SwimStroke stroke = default)
    {
        var density = waves.DensityOf(surface.Salt);
        var waterline = surface.Height - at.Feet;
        var air = body.Squeezed(body.BreathingLitres, surface.Height - (at.Feet + body.ChestMetres), density, WaveTheory.Gravity);
        var under = body.LitresUnder(waterline, air);
        var whole = body.Litres(air);
        var massKg = body.MassKg + kit.Kg;
        var addedKg = body.AddedKg(waterline, air, density);
        var inertia = massKg + addedKg;
        var addedAcrossKg = stroke.Stroking ? body.AddedAlongKg(waterline, air, density) : body.AddedAcrossKg(waterline, air, density);
        var inertiaAcross = massKg + addedAcrossKg;
        var thrust = stroke.Stroking ? config.Body.StrokeNewtons : 0;

        // the part of the body in a broken crest's water, which holds it up less
        var (churn, churned) = (0.0, 0.0);
        var now = sea.Seconds;
        if (breaker is { } crest)
        {
            if (crest.Spent(config.Breaking, now - crest.BrokeSeconds))
            {
                breaker = null;
            }
            else
            {
                churn = crest.AirShare(config.Breaking, now - crest.BrokeSeconds);
                var below = under - body.LitresUnder(waterline - crest.PlumeDepth(config.Breaking), air);
                churned = under > 0 ? below / under : 0;
            }
        }
        var held = (under + kit.Litres * under / whole) * (1 - churn * churned);

        var middle = Math.Max(0, surface.Height - (at.Feet + Math.Min(waterline, body.HeightMetres) / 2));
        var lift = Body.Lift(held, density, massKg, addedKg, WaveTheory.Gravity, sea.RiseAccelerationAt(at.X, at.Z, middle) * wavesPerSecond * wavesPerSecond);
        var rise = sea.RiseVelocityAt(at.X, at.Z, middle) * wavesPerSecond;
        var heave = Body.Dragged(motion.Y * WaterDrag.TicksPerSecond - rise, density, config.Body.DragAreaAlong, inertia, seconds) + rise
            + (lift + thrust * stroke.Y / inertia) * seconds;

        var top = surface.Swell - Math.Max(0, waterline - body.HeightMetres);
        var depth = Math.Min(waterline, body.HeightMetres);
        var flow = sea.HullVelocityAt(at.X, at.Z, top, depth, 0, at.Width, at.Width);
        flow = (flow.X * wavesPerSecond, flow.Z * wavesPerSecond);
        var gradient = sea.HullGradientAt(at.X, at.Z, top, depth, 0, at.Width, at.Width).Scaled(wavesPerSecond);
        var pressure = (density * held / Body.LitresPerCubicMetre + addedAcrossKg) / inertiaAcross;
        var before = carried;
        if (!met) (water, met) = (flow, true);
        var (shearX, shearZ) = gradient.Along(carried.X - water.X, carried.Z - water.Z);
        var (pushX, pushZ) = ((flow.X - water.X - shearX * seconds) * pressure, (flow.Z - water.Z - shearZ * seconds) * pressure);
        water = flow;
        var (rollerX, rollerZ) = (0.0, 0.0);
        if (breaker is { } roller)
        {
            var speed = churned * roller.RollerSpeed(config.Breaking, now - roller.BrokeSeconds) * wavesPerSecond;
            (rollerX, rollerZ) = (speed * Math.Cos(roller.TowardRadians), speed * Math.Sin(roller.TowardRadians));
        }
        var (toX, toZ) = (flow.X + rollerX, flow.Z + rollerZ);
        var (awayX, awayZ) = (carried.X + pushX - toX + thrust * stroke.X / inertiaAcross * seconds,
            carried.Z + pushZ - toZ + thrust * stroke.Z / inertiaAcross * seconds);
        var away = Math.Sqrt(awayX * awayX + awayZ * awayZ);
        var dragArea = stroke.Stroking ? config.Body.DragAreaSwimming : config.Body.DragAreaAcross * under / whole;
        var keptAway = away > 0 ? Body.Dragged(away, density, dragArea, inertiaAcross, seconds) / away : 1;
        carried = (toX + awayX * keptAway, toZ + awayZ * keptAway);

        Reading = new FloatReading(waterline, under, whole, air, lift, churn * churned, flow.X, rise, flow.Z, carried.X, carried.Z);
        return new BodyMotion(
            motion.X + WaterDrag.Carried(carried.X, before.X, 1, kept),
            heave / WaterDrag.TicksPerSecond,
            motion.Z + WaterDrag.Carried(carried.Z, before.Z, 1, kept));
    }
}

/// <summary>Where a body is: its feet in absolute block coordinates, the eye's height over them and its width, in metres.</summary>
public readonly record struct BodyPlace(double X, double Feet, double Z, double EyeHeight, double Width);

/// <summary>A body's motion, in blocks per tick, as vanilla's <c>Pos.Motion</c>.</summary>
public readonly record struct BodyMotion(double X, double Y, double Z);

/// <summary>What one step of a body found, for the log.</summary>
public readonly record struct FloatReading(double Waterline, double UnderLitres, double WholeLitres, double LungLitres, double Lift, double Churn,
    double FlowX, double Rise, double FlowZ, double CarriedX, double CarriedZ);
