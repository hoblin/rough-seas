using System;
using HarmonyLib;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace RoughSeas.Person;

/// <summary>
/// Floats this client's player by <see cref="FloatingBody"/>, in place of vanilla's gravity in water (a third of it while
/// still, none while the swimmer strokes) and vanilla's water drag up and down (a fixed share of the speed each tick, far stronger than
/// water's at a float's slow speeds). On the owning client alone: vanilla simulates a player's movement there.
/// </summary>
public static class BodyAfloat
{
    private const string HarmonyId = "roughseas.person.afloat";
    private const double LogEverySeconds = 5;

    private static Harmony? harmony;
    private static ICoreClientAPI? capi;
    private static PersonModSystem? system;
    private static IDrawnSea? drawn;
    private static DrawnWater? water;
    private static SeaWeatherSystem? sea;
    private static IPerson? person;
    private static PersonConfig config = new();
    private static Waves? waves;
    private static FloatingBody? body;
    private static bool liftedThisTick;
    private static double loggedAt = double.NegativeInfinity;

    public static void Attach(PersonModSystem mod, ICoreClientAPI client, IDrawnSea drawnSea, DrawnWater drawnWater, IPerson thePerson, PersonConfig personConfig, Waves theWaves)
    {
        if (harmony != null) return;
        (system, capi, drawn, water, person, config, waves) = (mod, client, drawnSea, drawnWater, thePerson, personConfig, theWaves);
        sea = client.ModLoader.GetModSystem<SeaWeatherSystem>();
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(PModuleGravity), nameof(PModuleGravity.DoApply)),
            prefix: new HarmonyMethod(typeof(BodyAfloat), nameof(Afloat)));
        harmony.Patch(AccessTools.Method(typeof(PModuleMotionDrag), nameof(PModuleMotionDrag.DoApply)),
            prefix: new HarmonyMethod(typeof(BodyAfloat), nameof(BeforeDrag)),
            postfix: new HarmonyMethod(typeof(BodyAfloat), nameof(AfterDrag)));
        mod.Log("a person's body: {0:F2} m, {1:F0} kg, {2:F1} L of tissue, {3:F2} L of air breathing at the surface",
            thePerson.Body.HeightMetres, thePerson.Body.MassKg, thePerson.Body.TissueLitres, thePerson.Body.BreathingLitres);
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
        (system, capi, drawn, water, sea, person, waves, body) = (null, null, null, null, null, null, null, null);
        (liftedThisTick, loggedAt) = (false, double.NegativeInfinity);
    }

    /// <summary>The server says a crest broke over this client's player.</summary>
    public static void Struck(BreakerPacket crest)
    {
        if (capi?.World.Player?.Entity is not { } entity || drawn == null) return;
        if (body == null)
        {
            system!.Log(entity, "a {0:F1} m crest broke here, but the body isn't afloat on this client", crest.HeightMetres);
            return;
        }
        body.Struck(drawn, crest);
        system!.Log(entity, "a {0:F1} m crest breaks over the swimmer, by the server: roller at {1:F1} m/s toward {2:F0}°", crest.HeightMetres,
            config.Breaking.RollerSpeedRatio * crest.Celerity, (crest.TowardRadians * 180 / Math.PI + 360) % 360);
    }

    [ReliesOn("Vintagestory.API.Common.Entities.PModuleGravity", "DoApply", "is the only gravity a player in water gets, a third of it still and none while the swimmer strokes")]
    [ReliesOn("Vintagestory.GameContent.EntityBehaviorPlayerPhysics", "SetModules", "steps gravity before motion drag, every tick on the client that moves the player")]
    private static bool Afloat(float dt, Entity entity, EntityPos pos)
    {
        if (capi == null || entity != capi.World.Player?.Entity) return true;
        if (!entity.Alive || entity is EntityAgent { MountedOn: not null } || pos.Dimension != Dimensions.NormalWorld)
        {
            Leave(entity, null);
            return true;
        }
        if (!(entity.FeetInLiquid || entity.Swimming) || water!.WaterAt(entity.World.BlockAccessor, pos.X, pos.Y, pos.Z) is not { } surface
            || surface.Height <= pos.Y)
        {
            Leave(entity, "out of the water");
            return true;
        }

        if (body == null)
        {
            body = new FloatingBody(person!, config, waves!);
            system!.Log(entity, "body in {0} water: held up by what it displaces, not vanilla's gravity", surface.Salt ? "sea" : "fresh");
        }
        var kit = person!.CarriedBy(entity);
        var place = new BodyPlace(pos.X, pos.Y, pos.Z, entity.LocalEyePos.Y, entity.CollisionBox.XSize);
        var rate = sea!.Clock.SecondsIn(dt, entity.World.Calendar.SpeedOfTime) / dt;
        var next = body.Step(drawn!, surface, place, new BodyMotion(pos.Motion.X, pos.Motion.Y, pos.Motion.Z), kit, dt, rate,
            WaterDrag.Kept(GlobalConstants.WaterDrag, dt));
        pos.Motion.Set(next.X, next.Y, next.Z);
        liftedThisTick = true;

        var now = capi.InWorldEllapsedMilliseconds / 1000.0;
        if (now - loggedAt >= LogEverySeconds)
        {
            loggedAt = now;
            var read = body.Reading;
            var eye = surface.Height - (pos.Y + place.EyeHeight);
            system!.Log(entity, "afloat at y {0:F2}: waterline {1:F2} m up the body, eye {2:F2} m {3}, {4:F1} of {5:F1} L under, lungs {6:F2} L, carrying {7:F1} kg, lift {8:F2} m/s2, water {9:F2} {10:F2} {11:F2} m/s, carried {12:F2} {13:F2} m/s{14}",
                pos.Y, read.Waterline, Math.Abs(eye), eye < 0 ? "above" : "under", read.UnderLitres, read.WholeLitres, read.LungLitres, kit.Kg, read.Lift,
                read.FlowX, read.Rise, read.FlowZ, read.CarriedX, read.CarriedZ, read.Churn > 0 ? $", {read.Churn:P1} of its water is air" : "");
        }
        return false;
    }

    [ReliesOn("Vintagestory.API.Common.Entities.PModuleMotionDrag", "DoApply", "scales the whole motion by WaterDrag^(33·seconds) while the feet are in water or the body swims")]
    private static void BeforeDrag(Entity entity, EntityPos pos, out double __state) => __state = pos.Motion.Y;

    // vanilla's water drag keeps the body's way across; up and down the body has had its own drag
    private static void AfterDrag(Entity entity, EntityPos pos, double __state)
    {
        if (!liftedThisTick || entity != capi?.World.Player?.Entity) return;
        liftedThisTick = false;
        pos.Motion.Y = __state;
    }

    private static void Leave(Entity entity, string? why)
    {
        if (body == null) return;
        body = null;
        if (why != null) system!.Log(entity, "body {0}: vanilla's gravity again", why);
    }
}
