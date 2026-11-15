using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Vintagestory.API.MathTools;

namespace RoughSeas.Compat.ExtremeWeather;

/// <summary>Extreme Weathers' members we reach by name, every one found when it is built.</summary>
public sealed class ExtremeWeatherMod
{
    private readonly FieldInfo config;
    private readonly Dictionary<string, FieldInfo> fields;
    private readonly FieldInfo tornadoes;
    private readonly MethodInfo spawnNear;

    public ExtremeWeatherMod(IEnumerable<string> settings)
    {
        var modSystem = Reach.Type("ExtremeWeather.ExtremeWeatherModSystem");
        var tornadoSystem = Reach.Type("ExtremeWeather.TornadoSystem");
        config = Reach.Field(modSystem, "Config");
        fields = settings.Append("TornadoChance").Distinct().ToDictionary(name => name, name => Reach.Field(config.FieldType, name));
        tornadoes = Reach.Field(tornadoSystem, "Instance");
        spawnNear = Reach.Method(tornadoSystem, "SpawnNear", typeof(Vec3d)).Returning(typeof(string));
        ApplyEventWind = Reach.Method(Reach.Type("ExtremeWeather.EventDirectorSystem"), "ApplyEventWind", typeof(Vec3d), typeof(Vec3d).MakeByRefType());
        Reach.Field(ApplyEventWind.DeclaringType!, "activeCode", typeof(string));
    }

    public MethodInfo ApplyEventWind { get; }

    private object Config => config.GetValue(null)!;

    public void Set(IReadOnlyDictionary<string, object> settings)
    {
        var current = Config;
        foreach (var (name, value) in settings)
        {
            var field = fields[name];
            field.SetValue(current, Convert.ChangeType(value, field.FieldType, CultureInfo.InvariantCulture));
        }
    }

    public object? Get(string name) => fields[name].GetValue(Config);

    public double TornadoChance => Convert.ToDouble(Get("TornadoChance"), CultureInfo.InvariantCulture);

    public string SpawnTornadoNear(Vec3d pos) =>
        tornadoes.GetValue(null) is { } system ? (string)spawnNear.Invoke(system, [pos])! : "EW's tornadoes are off";
}
