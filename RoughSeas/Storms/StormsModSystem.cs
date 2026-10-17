using System.Globalization;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using RoughSeas.SeaAndWeather;

namespace RoughSeas.Storms;

public class StormsModSystem : ModSystem
{
    private static readonly AssetLocation ConfigAsset = new("roughseas:config/storms.json");

    private ICoreAPI api = null!;

    public StormsConfig Config { get; private set; } = null!;

    public Waves Waves { get; private set; } = null!;

    public TickBudget Budget { get; } = new("storms");

    public override void Start(ICoreAPI api)
    {
        this.api = api;
        api.RegisterEntityBehaviorClass(EntityBehaviorSeaForces.Code, typeof(EntityBehaviorSeaForces));
        api.RegisterEntityBehaviorClass(EntityBehaviorBilge.Code, typeof(EntityBehaviorBilge));
        api.RegisterEntityBehaviorClass(EntityBehaviorMooring.Code, typeof(EntityBehaviorMooring));
        api.RegisterBlockBehaviorClass(BlockBehaviorBailer.Code, typeof(BlockBehaviorBailer));
        VesselDraught.Attach();
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        Config = api.Assets.Get<StormsConfig>(ConfigAsset);
        Waves = new Waves(Config.Waves);
    }

    public override void StartClientSide(ICoreClientAPI capi) => VesselMotion.Attach();

    public override void Dispose()
    {
        VesselDraught.Detach();
        if (api?.Side == EnumAppSide.Client) VesselMotion.Detach();
    }

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        new StormLightning(this, sapi.ModLoader.GetModSystem<SeaWeatherSystem>(), sapi).Start();
        Budget.CountTicks(sapi);
        sapi.ChatCommands.Create("storms")
            .WithDescription("Rough Seas: storms acting on vessels")
            .RequiresPrivilege(Privilege.controlserver)
            .BeginSubCommand("budget")
                .WithDescription("Server time the storms spent on vessels since the last report")
                .HandleWith(_ => TextCommandResult.Success(Budget.Report()))
            .EndSubCommand();
    }

    public void Log(Entity entity, string format, params object[] args) =>
        Log($"{entity.Code} {entity.EntityId} at {(int)entity.Pos.X} {(int)entity.Pos.Z}: {format}", args);

    public void Log(string format, params object[] args) =>
        Mod.Logger.Debug("[storms] " + string.Format(CultureInfo.InvariantCulture, format, args));

    public double GameHours(double realSeconds)
    {
        var calendar = api.World.Calendar;
        return realSeconds * calendar.SpeedOfTime * calendar.CalendarSpeedMul / 3600;
    }
}
