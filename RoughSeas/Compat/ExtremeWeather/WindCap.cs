using HarmonyLib;

namespace RoughSeas.Compat.ExtremeWeather;

/// <summary>
/// Extreme Weathers caps every wind at 1.05, below the 1.3 its own hurricane damage needs. The cap holds here only
/// while one of its events runs, so the damage, howl and shoving follow our storms' wind.
/// </summary>
public static class WindCap
{
    private const string HarmonyId = "roughseas.compat.extremeweather.windcap";

    private static readonly object patchLock = new();
    private static int attached;
    private static Harmony? harmony;

    public static void Attach()
    {
        lock (patchLock)
        {
            if (attached++ > 0) return;
            harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.Method("ExtremeWeather.EventDirectorSystem:ApplyEventWind"),
                prefix: new HarmonyMethod(typeof(WindCap), nameof(OnlyDuringEvents)));
        }
    }

    public static void Detach()
    {
        lock (patchLock)
        {
            if (--attached > 0 || harmony == null) return;
            harmony.UnpatchAll(HarmonyId);
            harmony = null;
        }
    }

    private static bool OnlyDuringEvents(string? ___activeCode) => ___activeCode != null;
}
