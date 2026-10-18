using System;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

namespace RoughSeas.SeaAndWeather;

public static class WindOnCoordinates
{
    private const string HarmonyId = "roughseas.seaandweather.coordinates";
    private const string TextKey = "text";
    private const double LineHeight = 24;
    private const double ExtraWidth = 40;

    private static readonly string[] Points = ["n", "nne", "ne", "ene", "e", "ese", "se", "sse", "s", "ssw", "sw", "wsw", "w", "wnw", "nw", "nnw"];

    private static Harmony? harmony;
    private static SeaWeatherSystem? sea;
    private static ICoreClientAPI? capi;

    public static void Attach(SeaWeatherSystem system, ICoreClientAPI api)
    {
        if (harmony != null) return;
        (sea, capi) = (system, api);
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(HudElementCoordinates), nameof(HudElementCoordinates.OnOwnPlayerDataReceived)),
            postfix: new HarmonyMethod(typeof(WindOnCoordinates), nameof(Grow)));
        harmony.Patch(AccessTools.Method(typeof(HudElementCoordinates), "Every250ms"),
            postfix: new HarmonyMethod(typeof(WindOnCoordinates), nameof(AddWind)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
        (sea, capi) = (null, null);
    }

    public static string Describe(SeaState state) =>
        Lang.Get("roughseas:wind-line", Lang.Get("roughseas:wind-from-" + Point(state.WindFromDegrees)), state.Beaufort, (int)Math.Round(state.WindSpeed));

    public static string Point(double fromDegrees) => Points[(int)Math.Round(fromDegrees / (360.0 / Points.Length)) % Points.Length];

    private static void Grow(HudElementCoordinates __instance)
    {
        var text = __instance.SingleComposer?.GetDynamicText(TextKey);
        if (text == null) return;
        foreach (var bounds in new[] { text.Bounds, text.Bounds.ParentBounds })
        {
            bounds.fixedHeight += LineHeight;
            bounds.fixedWidth += ExtraWidth;
        }
        __instance.SingleComposer.ReCompose();
    }

    private static void AddWind(HudElementCoordinates __instance)
    {
        if (sea == null || !__instance.IsOpened() || __instance.SingleComposer?.GetDynamicText(TextKey) is not { } text) return;
        if (capi?.World.Player?.Entity?.Pos is not { } pos) return;
        text.SetNewText(text.Text + "\n" + Describe(sea.At(pos.X, pos.Z)));
    }
}
