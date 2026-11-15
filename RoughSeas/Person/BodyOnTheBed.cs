using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Person;

/// <summary>
/// A swimming player whose feet stand on the bed stands as on land. On the client that moves the player, vanilla's walk
/// moves it across, while its jump is the legs' push (<see cref="Legs"/>), and its body turns upright as vanilla turns a
/// still one, walking or not. It is drawn so: vanilla's animation flags leave its Swim out, so vanilla's idle, walk and
/// jump play, on every client for every player it draws.
/// </summary>
public static class BodyOnTheBed
{
    private const string HarmonyId = "roughseas.person.bed";
    private const float UprightRadiansPerSecond = 1.2f;

    private static Harmony? harmony;
    private static PersonModSystem? system;

    public static void Attach(PersonModSystem mod)
    {
        if (harmony != null) return;
        system = mod;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(EntityAgent), nameof(EntityAgent.OnGameTick)),
            transpiler: new HarmonyMethod(typeof(BodyOnTheBed), nameof(SwimPose)));
        harmony.Patch(AccessTools.Method(typeof(EntityBehaviorPlayerPhysics), nameof(EntityBehaviorPlayerPhysics.SetPlayerControls)),
            prefix: new HarmonyMethod(typeof(BodyOnTheBed), nameof(BeforeControls)),
            postfix: new HarmonyMethod(typeof(BodyOnTheBed), nameof(Upright)));
        harmony.Patch(AccessTools.Method(typeof(PModuleOnGround), nameof(PModuleOnGround.Applicable)),
            postfix: new HarmonyMethod(typeof(BodyOnTheBed), nameof(Stands)));
        harmony.Patch(AccessTools.Method(typeof(PModuleOnGround), nameof(PModuleOnGround.DoApply)),
            prefix: new HarmonyMethod(typeof(BodyOnTheBed), nameof(BeforeWalk)),
            postfix: new HarmonyMethod(typeof(BodyOnTheBed), nameof(Walks)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        (harmony, system) = (null, null);
    }

    private static bool Swims(Entity entity) => entity.Swimming && !BodyAfloat.OnBed(entity);

    [ReliesOn("Vintagestory.API.Common.EntityAgent", "OnGameTick", "reads Swimming first in the activity flags it picks the player's animation by, and gives Jump while OnGround")]
    private static IEnumerable<CodeInstruction> SwimPose(IEnumerable<CodeInstruction> instructions)
    {
        var original = instructions.ToList();
        var matcher = new CodeMatcher(original).MatchStartForward(new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Entity), nameof(Entity.Swimming))));
        if (matcher.IsInvalid)
        {
            system?.Mod.Logger.Error("Vanilla's player animation no longer reads Swimming; a swimmer on the bed keeps the swim pose");
            return original;
        }
        return matcher.SetInstruction(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BodyOnTheBed), nameof(Swims)))).InstructionEnumeration();
    }

    private static void BeforeControls(EntityBehaviorPlayerPhysics __instance, out float __state) =>
        __state = __instance.entity is EntityPlayer player ? player.WalkPitch : 0;

    [ReliesOn("Vintagestory.GameContent.EntityBehaviorPlayerPhysics", "SetPlayerControls", "turns WalkPitch toward the camera while the player swims and moves, and uprights it on the bed at 1.2 rad/s only while still")]
    private static void Upright(EntityBehaviorPlayerPhysics __instance, float dt, float __state)
    {
        if (__instance.entity is not EntityPlayer player || !BodyAfloat.OnBed(player)) return;
        var step = UprightRadiansPerSecond * dt * GlobalConstants.OverallSpeedMultiplier;
        var distance = GameMath.AngleRadDistance(__state, 0);
        player.WalkPitch = GameMath.Mod(__state + GameMath.Clamp(distance, -step, step), GameMath.TWOPI);
    }

    [ReliesOn("Vintagestory.API.Common.Entities.PModuleOnGround", "Applicable", "keeps vanilla's walk off a player who swims, its feet on the bed or not")]
    private static void Stands(Entity entity, ref bool __result) => __result |= BodyAfloat.OnBed(entity);

    private static void BeforeWalk(EntityPos pos, out double __state) => __state = pos.Motion.Y;

    [ReliesOn("Vintagestory.API.Common.Entities.PModuleOnGround", "DoApply", "walks by WalkVector across alone and jumps by setting Motion.Y")]
    private static void Walks(Entity entity, EntityPos pos, double __state)
    {
        if (BodyAfloat.OnBed(entity)) pos.Motion.Y = __state;
    }
}
