using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RoughSeas.SeaAndWeather;

public class SeaWeatherSystem : ModSystem, ISeaWeather
{
    private ICoreAPI api = null!;
    private Lazy<SeaWeatherModel> model = null!;
    private Lazy<SeaWeatherConfig> config = null!;

    public TickBudget Budget { get; } = new();

    public override double ExecuteOrder() => 1;

    public override void Start(ICoreAPI api)
    {
        this.api = api;
        config = new Lazy<SeaWeatherConfig>(() => api.Assets.Get<SeaWeatherConfig>(new AssetLocation("roughseas", "config/seaweather.json")));
        model = new Lazy<SeaWeatherModel>(CreateModel);
        VanillaWeatherPatches.Attach(this, api.Side);
    }

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        new VanillaWeather(this, sapi).FollowOnServer(sapi);
        Budget.CountTicks(sapi);
        SeaWeatherCommands.Register(this, sapi);
    }

    public override void StartClientSide(ICoreClientAPI capi)
    {
        new VanillaWeather(this, capi);
        SeaWeatherCommands.Register(this, capi);
    }

    public override void Dispose() => VanillaWeatherPatches.Detach(api.Side);

    public bool DangerousOceans => api.World.Config.GetBool("dangerousOceans", true);

    public SeaWeatherConfig Config => config.Value;

    public double NowHours => api.World.Calendar.TotalHours;

    public SeaState At(double x, double z) => At(x, z, NowHours);

    public SeaState At(double x, double z, double totalHours) => model.Value.At(x, z, totalHours, Oceanity(x, z));

    public IEnumerable<Storm> StormsWithin(double x, double z, double range) => model.Value.Storms.Active(x, z, NowHours, range);

    public WindSample Wind(double x, double z) => model.Value.Wind(x, z, NowHours);

    public WindSample Wind(double x, double z, double totalHours) => model.Value.Wind(x, z, totalHours);

    private double Oceanity(double x, double z)
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
            z => calendar.OnGetLatitude?.Invoke(z) ?? 0.5,
            calendar.DaysPerYear * calendar.HoursPerDay);
    }
}
