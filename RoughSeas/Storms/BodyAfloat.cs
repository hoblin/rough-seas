using System;
using HarmonyLib;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

/// <summary>
/// Floats this client's player by the water their body displaces under the drawn surface, in place of vanilla's
/// gravity in water (a third of it while still, none while swimming). The body's weight, its worn armour's and the
/// water's under the surface (<see cref="Body"/>) set how it rises, how deep it lies and how it heaves, against the
/// water's drag along the body's length, in place of vanilla's water drag up and down (a fixed share of the speed each
/// tick, far stronger than water's at a float's slow speeds); the water's own motion under the waves carries it
/// across through vanilla's water drag, which runs right after, and up and down through its own; and a crest that
/// breaks over it (<see cref="Breaker"/>) churns air into the water and runs its roller along the wave, so the body
/// sinks and is carried with it until the water clears. On a client alone, where a player's own movement is simulated.
/// </summary>
public static class BodyAfloat
{
    private const string HarmonyId = "roughseas.storms.afloat";
    private const double Gravity = 9.81;
    private const double TicksPerSecond = 60;
    private const double LogEverySeconds = 5;

    private static Harmony? harmony;
    private static StormsModSystem? storms;
    private static ICoreClientAPI? capi;
    private static DrawnSea? drawn;
    private static DrawnWater? water;
    private static SeaWeatherSystem? sea;
    private static BodyConfig config = new();
    private static Body? body;
    private static readonly CrestWatch crests = new();
    private static Breaker? breaker;
    private static double lungs = double.NaN;
    private static bool afloat;
    private static bool liftedThisTick;
    private static double loggedAt = double.NegativeInfinity;

    public static void Attach(StormsModSystem system, ICoreClientAPI client, DrawnSea sea, DrawnWater drawnWater, BodyConfig bodyConfig)
    {
        if (harmony != null) return;
        (storms, capi, drawn, water, config) = (system, client, sea, drawnWater, bodyConfig);
        BodyAfloat.sea = client.ModLoader.GetModSystem<SeaWeatherSystem>();
        body = Body.Of(config);
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(PModuleGravity), nameof(PModuleGravity.DoApply)),
            prefix: new HarmonyMethod(typeof(BodyAfloat), nameof(Afloat)));
        harmony.Patch(AccessTools.Method(typeof(PModuleMotionDrag), nameof(PModuleMotionDrag.DoApply)),
            prefix: new HarmonyMethod(typeof(BodyAfloat), nameof(BeforeDrag)),
            postfix: new HarmonyMethod(typeof(BodyAfloat), nameof(AfterDrag)));
        system.Log("a swimmer's body: {0:F1} m, {1:F0} kg, {2:F1} L of tissue, {3:F1} L of air breathing at the surface",
            body.HeightMetres, body.MassKg, body.TissueLitres, body.BreathingLitres);
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
        (storms, capi, drawn, water, sea, body, breaker) = (null, null, null, null, null, null, null);
        (lungs, afloat, liftedThisTick, loggedAt) = (double.NaN, false, false, double.NegativeInfinity);
        crests.Reset();
    }

    // runs where vanilla would apply its gravity, before its water drag; false skips vanilla's gravity
    private static bool Afloat(float dt, Entity entity, EntityPos pos)
    {
        if (capi == null || body == null || entity != capi.World.Player?.Entity || !entity.Alive || entity is EntityAgent { MountedOn: not null }
            || water!.WaterAt(entity.World.BlockAccessor, pos.X, pos.Y, pos.Z) is not { } surface || surface.Height <= pos.Y)
        {
            Ashore(entity);
            return true;
        }

        var now = Seconds;
        var waterline = surface.Height - pos.Y;
        var eye = pos.Y + entity.LocalEyePos.Y;
        // breathing at the surface; under it the breath is held as it was
        if (double.IsNaN(lungs)) lungs = body.BreathingLitres;
        if (eye > surface.Height) lungs += (body.BreathingLitres - lungs) * (1 - Math.Exp(-dt / config.BreathSeconds));

        var middle = Math.Max(0, surface.Height - (pos.Y + Math.Min(waterline, body.HeightMetres) / 2));
        Break(entity, pos, surface, now);
        var density = surface.Salt ? config.SeawaterDensity : config.FreshwaterDensity;
        var armourKg = Mass?.ArmorKg(entity) ?? 0;
        var under = body.LitresUnder(waterline, lungs);
        var armourUnder = armourKg / config.ArmourDensity * under / body.Litres(lungs);
        var massKg = body.MassKg + armourKg;

        // the part of the body in a broken crest's churned water: that water, full of air, holds it up less
        var (air, churnedShare) = (0.0, 0.0);
        if (breaker is { } crest)
        {
            air = crest.AirShare(config.Breaking, now - crest.BrokeSeconds);
            var churned = under - body.LitresUnder(waterline - crest.PlumeDepth(config.Breaking), lungs);
            churnedShare = under > 0 ? churned / under : 0;
        }
        var held = (under + armourUnder) * (1 - air * churnedShare);

        var lift = Body.Lift(held, density, massKg, config.AddedMass, Gravity, drawn!.RiseAccelerationAt(pos.X, pos.Z, middle));
        var rise = drawn.RiseVelocityAt(pos.X, pos.Z, middle);
        var heave = Body.Dragged(pos.Motion.Y * TicksPerSecond - rise, held, density, massKg, config, dt) + rise + lift * dt;
        pos.Motion.Y = heave / TicksPerSecond;
        liftedThisTick = true;

        // the water's own motion across, which vanilla's drag pulls the body toward
        var (flowX, flowZ) = drawn.OrbitalVelocityAt(pos.X, pos.Z, middle, entity.CollisionBox.XSize);
        if (breaker is { } roller)
        {
            var speed = churnedShare * roller.RollerSpeed(config.Breaking, now - roller.BrokeSeconds);
            flowX += speed * Math.Cos(roller.TowardRadians);
            flowZ += speed * Math.Sin(roller.TowardRadians);
        }
        pos.Motion.X += Carried(flowX, dt);
        pos.Motion.Z += Carried(flowZ, dt);

        if (!afloat)
        {
            afloat = true;
            storms!.Log(entity, "body in {0} water: held up by what it displaces, not vanilla's gravity", surface.Salt ? "sea" : "fresh");
        }
        if (now - loggedAt >= LogEverySeconds)
        {
            loggedAt = now;
            storms!.Log(entity, "afloat: waterline {0:F2} m up the body, eye {1:F2} m {2}, {3:F1} of {4:F1} L under, lungs {5:F1} L, armour {6:F0} kg, lift {7:F2} m/s2, water {8:F2} {9:F2} {10:F2} m/s{11}",
                waterline, Math.Abs(eye - surface.Height), eye > surface.Height ? "above" : "under", under, body.Litres(lungs), lungs,
                armourKg, lift, flowX, rise, flowZ, air * churnedShare > 0 ? $", {air:P0} air in the water around {churnedShare:P0} of it" : "");
        }
        return false;
    }

    /// <summary>
    /// What the body's motion takes this step, in blocks per physics tick, from water moving across at
    /// <paramref name="metresPerSecond"/>, when vanilla's water drag runs right after and keeps a share of the motion.
    /// Vanilla drags a body toward rest; with this added the drag pulls it toward the water instead, and a body left
    /// to it comes to the water's velocity.
    /// </summary>
    internal static double Carried(double metresPerSecond, double seconds)
    {
        var kept = WaveCarry.Kept(GlobalConstants.WaterDrag, seconds);
        return metresPerSecond / TicksPerSecond * (1 - kept) / kept;
    }

    private static void BeforeDrag(Entity entity, EntityPos pos, out double __state) => __state = pos.Motion.Y;

    // vanilla's water drag keeps the body's way across; up and down the body has had its own drag
    private static void AfterDrag(Entity entity, EntityPos pos, double __state)
    {
        if (!liftedThisTick || entity != capi?.World.Player?.Entity) return;
        liftedThisTick = false;
        pos.Motion.Y = __state;
    }

    private static void Break(Entity entity, EntityPos pos, WaterSurface surface, double now)
    {
        if (breaker is { } last && last.Spent(config.Breaking, now - last.BrokeSeconds))
        {
            breaker = null;
            storms!.Log(entity, "the broken crest's water has cleared");
        }
        if (crests.Passed(surface.Swell) is not { } crest || breaker != null || sea is not { DangerousOceans: true }) return;

        var waves = storms!.Waves;
        var height = crest / storms.Config.Waves.CrestRatio;
        if (height < storms.Config.Waves.MinHeight) return;
        var state = sea.At(pos.X, pos.Z);
        var chance = waves.BreakingChance(state.WindSpeed);
        if (entity.World.Rand.NextDouble() >= chance) return;

        var celerity = waves.BreakerCelerity(height, state.WavePeriod, double.PositiveInfinity);
        breaker = new Breaker(now, height, state.WavePeriod, celerity, drawn!.WavesTowardRadians);
        storms.Log(entity, "a {0:F1} m crest breaks over the swimmer (wind {1:F1} m/s, {2:P1} of waves break): {3:P0} air in the water to {4:F1} m deep, its roller at {5:F1} m/s toward {6:F0}°",
            height, state.WindSpeed, chance, config.Breaking.Aeration, config.Breaking.PlumeDepthPerHeight * height,
            config.Breaking.RollerSpeedRatio * celerity, (drawn.WavesTowardRadians * 180 / Math.PI + 360) % 360);
    }

    private static void Ashore(Entity entity)
    {
        if (!afloat || entity != capi?.World.Player?.Entity) return;
        afloat = false;
        (breaker, lungs) = (null, double.NaN);
        crests.Reset();
        storms!.Log(entity, "body out of the water: vanilla's gravity again");
    }

    private static VesselMass? Mass => capi?.ModLoader.GetModSystem<VesselsModSystem>()?.Mass;

    private static double Seconds => capi!.InWorldEllapsedMilliseconds / 1000.0;
}
