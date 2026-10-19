using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Soundscape;

public static class VanillaRowing
{
    private const string HarmonyId = "roughseas.soundscape.rowing";

    private static Harmony? harmony;
    private static float volume = 1;

    public static void Attach(float rowingVolume)
    {
        if (harmony != null) return;
        volume = rowingVolume;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(ModSystemBoatingSoundAndRatlineStamina), nameof(ModSystemBoatingSoundAndRatlineStamina.NowInMotion)),
            transpiler: new HarmonyMethod(typeof(VanillaRowing), nameof(Quieter)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
    }

    private static IEnumerable<CodeInstruction> Quieter(IEnumerable<CodeInstruction> instructions) =>
        new CodeMatcher(instructions)
            .MatchEndForward(new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(GameMath), nameof(GameMath.Clamp), [typeof(float), typeof(float), typeof(float)])))
            .ThrowIfInvalid("vanilla's rowing volume is no longer GameMath.Clamp(float, float, float)")
            .Advance(1)
            .Insert(new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(VanillaRowing), nameof(volume))), new CodeInstruction(OpCodes.Mul))
            .InstructionEnumeration();
}
