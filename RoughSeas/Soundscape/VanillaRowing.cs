using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Soundscape;

public static class VanillaRowing
{
    private const string HarmonyId = "roughseas.soundscape.rowing";

    private static Harmony? harmony;
    private static SoundscapeModSystem? sounds;
    private static float volume = 1;

    public static void Attach(float rowingVolume, SoundscapeModSystem soundscape)
    {
        if (harmony != null) return;
        (volume, sounds) = (rowingVolume, soundscape);
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(ModSystemBoatingSoundAndRatlineStamina), nameof(ModSystemBoatingSoundAndRatlineStamina.NowInMotion)),
            transpiler: new HarmonyMethod(typeof(VanillaRowing), nameof(Quieter)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        (harmony, sounds) = (null, null);
    }

    private static IEnumerable<CodeInstruction> Quieter(IEnumerable<CodeInstruction> instructions)
    {
        var original = instructions.ToList();
        var matcher = new CodeMatcher(original)
            .MatchEndForward(new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(GameMath), nameof(GameMath.Clamp), [typeof(float), typeof(float), typeof(float)])));
        if (matcher.IsInvalid)
        {
            sounds?.Mod.Logger.Error("Vanilla's rowing sound changed; it is left at its own volume");
            return original;
        }
        sounds?.Log("vanilla's rowing sound plays at {0:F2} of its volume", volume);
        return matcher.Advance(1)
            .Insert(new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(VanillaRowing), nameof(volume))), new CodeInstruction(OpCodes.Mul))
            .InstructionEnumeration();
    }
}
