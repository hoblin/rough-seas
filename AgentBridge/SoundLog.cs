using System;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using static System.FormattableString;

namespace AgentBridge;

public sealed class SoundLog : IDisposable
{
    private const string Sound = "Vintagestory.Client.LoadedSoundNative";

    private static ICoreClientAPI? capi;
    private static ILogger? logger;
    private readonly Harmony harmony = new("agentbridge.soundlog");

    public SoundLog(ICoreClientAPI api, ILogger log)
    {
        (capi, logger) = (api, log);
        harmony.Patch(AccessTools.Method(Sound + ":Start"), postfix: new HarmonyMethod(typeof(SoundLog), nameof(Started)));
        harmony.Patch(AccessTools.Method(Sound + ":Stop"), prefix: new HarmonyMethod(typeof(SoundLog), nameof(Stopping)));
        harmony.Patch(AccessTools.Method(Sound + ":FadeTo"), prefix: new HarmonyMethod(typeof(SoundLog), nameof(Fading)));
    }

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        (capi, logger) = (null, null);
    }

    private static void Started(ILoadedSound __instance) =>
        Log(__instance, Invariant($"starts at {__instance.Params.Volume:F3}, ") +
            (__instance.Params.ShouldLoop ? "looped" : Invariant($"{__instance.SoundLengthSeconds:F1} s long")));

    private static void Stopping(ILoadedSound __instance)
    {
        if (__instance.IsPlaying) Log(__instance, "stops");
    }

    private static void Fading(ILoadedSound __instance, double newVolume, float duration) =>
        Log(__instance, Invariant($"fades from {__instance.Params.Volume:F3} to {newVolume:F3} over {duration:F1} s"));

    private static void Log(ILoadedSound loaded, string what)
    {
        var sound = loaded.Params;
        var listener = capi?.World?.Player?.Entity?.Pos;
        var where = sound.RelativePosition || sound.Position == null || listener == null
            ? "on the listener"
            : Invariant($"{Math.Sqrt(listener.SquareDistanceTo(sound.Position.X, sound.Position.Y, sound.Position.Z)):F1} m away");
        logger?.Debug(Invariant($"[sound] {sound.Location} ({sound.SoundType}, {where}) {what}"));
    }
}
