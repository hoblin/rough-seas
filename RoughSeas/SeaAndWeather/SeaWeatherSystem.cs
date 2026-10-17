using System;
using System.Collections.Generic;
using System.Globalization;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RoughSeas.SeaAndWeather;

public class SeaWeatherSystem : ModSystem, ISeaWeather
{
    private ICoreAPI api = null!;
    private Lazy<SeaWeatherModel> model = null!;
    private Lazy<SeaWeatherConfig> config = null!;
    private VanillaWeather vanilla = null!;

    public TickBudget Budget { get; } = new("sea and weather");

    /// <summary>After vanilla's weather systems (0.1), before the Devastation's effects (2).</summary>
    public override double ExecuteOrder() => 1;

    public override void Start(ICoreAPI api)
    {
        this.api = api;
        config = new Lazy<SeaWeatherConfig>(() => api.Assets.Get<SeaWeatherConfig>(new AssetLocation("roughseas", "config/seaweather.json")));
        model = new Lazy<SeaWeatherModel>(CreateModel);
        vanilla = new VanillaWeather(this, api);
        vanilla.FollowWind();
        VanillaWeatherPatches.Attach(this, api.Side);
        api.RegisterEntityBehaviorClass(CurrentDrift.Code, typeof(CurrentDrift));
    }

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        vanilla.FollowStormEvents(sapi);
        Budget.CountTicks(sapi);
        SeaWeatherCommands.Register(this, sapi);
    }

    public override void StartClientSide(ICoreClientAPI capi) => SeaWeatherCommands.Register(this, capi);

    public override void Dispose() => VanillaWeatherPatches.Detach(api.Side);

    public bool DangerousOceans => api.World.Config.GetBool("dangerousOceans", true);

    public SeaWeatherConfig Config => config.Value;

    public double NowHours => api.World.Calendar.TotalHours;

    public SeaState At(double x, double z) => At(x, z, NowHours);

    public SeaState At(double x, double z, double totalHours) => model.Value.At(x, z, totalHours, Oceanity(x, z));

    public SeaCurrent CurrentAt(double x, double z) => model.Value.Current(x, z);

    public bool IsSea(double x, double z) => model.Value.IsSea(x, z);

    public IEnumerable<Storm> StormsWithin(double x, double z, double range) => model.Value.Storms.Active(x, z, NowHours, range);

    public WindSample Wind(double x, double z) => model.Value.Wind(x, z, NowHours);

    public WindSample StormsAt(double x, double z, double totalHours) => model.Value.StormsAt(x, z, totalHours);

    public float RainCloudness(double x, double z, double totalDays)
    {
        using var _ = Budget.Measure();
        return (float)model.Value.RainCloudness(x, z, totalDays * api.World.Calendar.HoursPerDay);
    }

    public void Log(string format, params object[] args) =>
        Mod.Logger.Debug("[seas] " + string.Format(CultureInfo.InvariantCulture, format, args));

    public double Oceanity(double x, double z)
    {
        var accessor = api.World.BlockAccessor;
        var regionSize = accessor.RegionSize;
        var map = accessor.GetMapRegion((int)x / regionSize, (int)z / regionSize)?.OceanMap;
        return map is { Data.Length: > 0 }
            ? map.GetUnpaddedIntLerpedForBlockPos((int)x, (int)z, regionSize) / 255.0
            : 1;
    }

    private SeaWeatherModel CreateModel()
    {
        var world = api.World;
        var calendar = world.Calendar;
        return new SeaWeatherModel(
            world.Seed,
            Config,
            new WorldClimate(world.Seed, world.Config, world.BlockAccessor.MapSizeZ),
            new OceanBasins(world.Seed, world.Config, world.BlockAccessor.MapSizeX, world.BlockAccessor.MapSizeZ, Config.Currents),
            z => calendar.OnGetLatitude?.Invoke(z) ?? 0.5,
            calendar.DaysPerYear * calendar.HoursPerDay);
    }
}
