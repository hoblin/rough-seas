using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using Vintagestory.API.MathTools;

namespace RoughSeas.Compat.ExtremeWeather;

/// <summary>Extreme Weathers' members we reach by name, as its tested versions have them: see config/compat/extremeweather.json.</summary>
public static class ExtremeWeatherMod
{
    private static readonly Type ModSystem = AccessTools.TypeByName("ExtremeWeather.ExtremeWeatherModSystem");
    private static readonly Type TornadoSystem = AccessTools.TypeByName("ExtremeWeather.TornadoSystem");

    private static object Config => AccessTools.Field(ModSystem, "Config").GetValue(null)!;

    public static void Set(IReadOnlyDictionary<string, object> settings)
    {
        var config = Config;
        foreach (var (name, value) in settings)
        {
            var field = AccessTools.Field(config.GetType(), name);
            field.SetValue(config, Convert.ChangeType(value, field.FieldType, CultureInfo.InvariantCulture));
        }
    }

    public static object? Get(string name) => AccessTools.Field(Config.GetType(), name).GetValue(Config);

    public static double TornadoChance => Convert.ToDouble(Get("TornadoChance"), CultureInfo.InvariantCulture);

    public static string SpawnTornadoNear(Vec3d pos) =>
        AccessTools.Field(TornadoSystem, "Instance").GetValue(null) is { } tornadoes
            ? (string)AccessTools.Method(TornadoSystem, "SpawnNear").Invoke(tornadoes, [pos])!
            : "EW's tornadoes are off";
}
