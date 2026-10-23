using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.Server;
using Vintagestory.ServerMods;

namespace RoughSeas.SeaAndWeather;

public class SeaWeatherSystem : ModSystem, ISeaWeather
{
    private const double DiurnalMeanHour = 10;
    public const string Channel = "roughseas:seamap";

    private const string CalibrationKey = "roughseas:oceancalibration";

    private ICoreAPI api = null!;
    private Lazy<SeaWeatherConfig> config = null!;
    private Lazy<SeaModel>? model;
    private SeaMapClient? map;
    private OceanCalibration? calibration;
    private readonly HashSet<(int X, int Z)> calibrated = [];
    private WorldOceanMap? ocean;

    public TickBudget Budget { get; } = new("sea and weather");

    /// <summary>After vanilla's weather systems (0.1), before the Devastation's effects (2).</summary>
    public override double ExecuteOrder() => 1;

    public override void Start(ICoreAPI api)
    {
        this.api = api;
        Clock = new SeaClock(() => api.World?.Calendar);
        config = new Lazy<SeaWeatherConfig>(() => api.Assets.Get<SeaWeatherConfig>(new AssetLocation("roughseas", "config/seaweather.json")));
        api.Network.RegisterChannel(Channel).RegisterMessageType<SeaMapSnapshot>();
        new VanillaWind(this, api).Follow();
        VanillaWeatherPatches.Attach(this, api.Side);
        api.RegisterEntityBehaviorClass(CurrentDrift.Code, typeof(CurrentDrift));
    }

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        calibration = new OceanCalibration(Config.Ocean);
        model = new Lazy<SeaModel>(() => CreateModel(sapi));
        sapi.Event.SaveGameLoaded += () => calibration.Load(sapi.WorldManager.SaveGame.GetData(CalibrationKey));
        sapi.Event.GameWorldSave += () => sapi.WorldManager.SaveGame.StoreData(CalibrationKey, calibration.ToBytes());
        new VanillaRain(this, sapi).Follow();
        new VanillaRegions(this, sapi).Drive();
        new SeaMapServer(this, sapi).Start();
        Budget.CountTicks(sapi);
        sapi.Event.RegisterGameTickListener(dt => Clock.Track(dt), 0);
        SeaWeatherCommands.Register(this, sapi);
    }

    public override void StartClientSide(ICoreClientAPI capi)
    {
        map = new SeaMapClient(this, capi);
        map.Start();
        capi.Event.RegisterGameTickListener(dt => Clock.Track(dt), 0);
        new VanillaRain(this, capi).Follow();
        WindOnCoordinates.Attach(this, capi);
        SeaWeatherCommands.Register(this, capi);
    }

    public override void Dispose()
    {
        VanillaWeatherPatches.Detach(api.Side);
        if (api.Side == EnumAppSide.Client) WindOnCoordinates.Detach();
        if (api.Side == EnumAppSide.Server) OceanGenLock.Detach();
    }

    public bool DangerousOceans => api.World.Config.GetBool("dangerousOceans", true);

    public SeaWeatherConfig Config => config.Value;

    public SeaClock Clock { get; private set; } = null!;
    /// <summary>The model, on the server; a client reads the map the server sends instead.</summary>
    public SeaModel Model => model?.Value ?? throw new InvalidOperationException("The sea model runs on the server alone");

    public ISeaSource Source => map ?? (ISeaSource)Model;

    public string OceanReport(ICoreServerAPI sapi, double x, double z)
    {
        var (water, land) = calibration!.Seen;
        var value = Ocean(sapi).At((int)Math.Floor(x / WorldOceanMap.PixelSize), (int)Math.Floor(z / WorldOceanMap.PixelSize));
        var wind = Model.Wind(x, z);
        var reach = Model.Upwind(x, z, Compass.Radians(wind.X, wind.Z));
        var reading = calibration.Settled ? "calibrated against the built world" : "the default until the built world shows enough of both";
        return $"ocean map {value} of 255 here, {(Model.IsSea(x, z) ? "open sea" : "not open sea")}; open sea from {calibration.SeaFrom} "
            + $"({reading}: {water} sea and {land} land columns seen); {Model.WaterHere(x, z)}; "
            + $"upwind: {reach.FetchMetres / 1000:0.0} km of fetch over {reach.MeanDepthMetres:0} m of water";
    }

    public SeaState At(double x, double z) => Source.At(x, z);

    public SeaCurrent CurrentAt(double x, double z) => Source.CurrentAt(x, z);

    public void Log(string format, params object[] args) =>
        Mod.Logger.Debug("[seas] " + string.Format(CultureInfo.InvariantCulture, format, args));

    private SeaModel CreateModel(ICoreServerAPI sapi)
    {
        var world = sapi.World;
        var calendar = world.Calendar;
        var basins = new OceanBasins(Ocean(sapi), calibration!, Config.Currents);
        var blockMetres = Config.Waves.RealPolarEquatorMetres / world.Config.GetString("polarEquatorDistance", "50000").ToInt(50000);
        var climate = new WorldClimate(world.Seed, world.Config, world.BlockAccessor.MapSizeZ);
        var weather = new SeaWeatherModel(
            world.Seed,
            Config,
            climate,
            basins,
            z => calendar.OnGetLatitude?.Invoke(z) ?? 0.5,
            calendar.DaysPerYear * calendar.HoursPerDay,
            blockMetres,
            (x, z, hours) => SeasonalCelsius(sapi, climate, x, z, hours));
        return new SeaModel(this, sapi, weather, new WaterBody(sapi, basins, Config.Waves, blockMetres, () => Clock.Hours));
    }

    private static double SeasonalCelsius(ICoreServerAPI sapi, WorldClimate climate, double x, double z, double hours)
    {
        var (celsius, rainfall) = climate.At(x, z);
        var condition = new ClimateCondition { WorldGenTemperature = (float)celsius, Temperature = (float)celsius, Rainfall = (float)rainfall, WorldgenRainfall = (float)rainfall };
        var days = Math.Floor(hours / sapi.World.Calendar.HoursPerDay) + DiurnalMeanHour / 24;
        ((ServerMain)sapi.World).EventManager.TriggerOnGetClimate(ref condition, new BlockPos((int)x, sapi.World.SeaLevel, (int)z, Dimensions.NormalWorld),
            EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, days);
        return condition.Temperature;
    }

    private WorldOceanMap Ocean(ICoreServerAPI sapi) => ocean ??= new WorldOceanMap(sapi, new GeneratedOcean(() => Generator(sapi)));

    private MapLayerBase? Generator(ICoreServerAPI sapi)
    {
        var generator = sapi.ModLoader.GetModSystem<GenMaps>()?.oceanGen;
        if (generator == null) return null;
        OceanGenLock.Attach(generator);
        Log("ocean map: built regions' own, and the world's {0} where not built", generator.GetType().Name);
        return generator;
    }

    public void Calibrate(ICoreServerAPI sapi, int x, int z)
    {
        if (calibration is not { Settled: false }) return;
        var surface = new BlockPos(x, sapi.World.SeaLevel - 1, z, Dimensions.NormalWorld);
        if (sapi.World.BlockAccessor.GetChunkAtBlockPos(surface) == null || !calibrated.Add((x, z))) return;
        var water = sapi.World.BlockAccessor.GetBlock(surface, BlockLayersAccess.Fluid).LiquidCode == "saltwater";
        if (!calibration.Add(Ocean(sapi).At(x / WorldOceanMap.PixelSize, z / WorldOceanMap.PixelSize), water)) return;
        Log("ocean map calibrated against the built world: open sea from {0} of 255", calibration.SeaFrom);
        if (model is { IsValueCreated: true }) model.Value.Forget();
    }
}
