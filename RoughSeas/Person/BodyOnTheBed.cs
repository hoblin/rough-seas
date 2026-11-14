using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Person;

/// <summary>
/// A swimming player whose feet stand on the bed is drawn as on land: vanilla's animation flags leave its Swim out, so
/// vanilla's idle, walk and jump play, and its body turns upright. On every client, for every player it draws.
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
            postfix: new HarmonyMethod(typeof(BodyOnTheBed), nameof(Upright)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        (harmony, system) = (null, null);
    }

    public static bool Stands(Entity entity) =>
        entity is EntityPlayer { Swimming: true, OnGround: true } && Mechanic.RealisticSwimming.On(entity.World);

    private static bool Swims(Entity entity) => entity.Swimming && !Stands(entity);

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

    [ReliesOn("Vintagestory.GameContent.EntityBehaviorPlayerPhysics", "SetPlayerControls", "turns WalkPitch toward the camera while the player swims and moves, and uprights it slowly on the bed only while still")]
    private static void Upright(EntityBehaviorPlayerPhysics __instance, float dt)
    {
        if (__instance.entity is not EntityPlayer player || !Stands(player)) return;
        var distance = GameMath.AngleRadDistance(player.WalkPitch, 0);
        player.WalkPitch = GameMath.Mod(player.WalkPitch + GameMath.Clamp(distance, -UprightRadiansPerSecond * dt, UprightRadiansPerSecond * dt), GameMath.TWOPI);
    }
}
