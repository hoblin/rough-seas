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
    private SurfaceClock? clock;

    public StormsConfig Config { get; private set; } = null!;

    public Waves Waves { get; private set; } = null!;

    public TickBudget Budget { get; } = new("storms");

    /// <summary>The sea surface this client draws; null on the server.</summary>
    public IDrawnSea? DrawnSea { get; private set; }

    /// <summary>The water a point is in by <see cref="DrawnSea"/>; null on the server.</summary>
    public DrawnWater? DrawnWater { get; private set; }

    /// <summary>The surface behind <see cref="DrawnSea"/>, foam included; null on the server.</summary>
    internal SeaSurface? Surface { get; private set; }

    public override void Start(ICoreAPI api)
    {
        this.api = api;
        api.RegisterEntityBehaviorClass(EntityBehaviorSeaForces.Code, typeof(EntityBehaviorSeaForces));
        api.RegisterEntityBehaviorClass(EntityBehaviorBilge.Code, typeof(EntityBehaviorBilge));
        api.RegisterEntityBehaviorClass(EntityBehaviorMooring.Code, typeof(EntityBehaviorMooring));
        api.RegisterEntityBehaviorClass(EntityBehaviorCurrentDrift.Code, typeof(EntityBehaviorCurrentDrift));
        api.RegisterBlockBehaviorClass(BlockBehaviorBailer.Code, typeof(BlockBehaviorBailer));
        VesselDraught.Attach();
        HullDrift.Attach();
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        Config = api.Assets.Get<StormsConfig>(ConfigAsset);
        Waves = new Waves(Config.Waves);
    }

    public override void StartClientSide(ICoreClientAPI capi)
    {
        var sea = capi.ModLoader.GetModSystem<SeaWeatherSystem>();
        var surface = Surface = new SeaSurface(Config.Surface, Waves);
        var field = new SurfaceField();
        surface.Changed += change => Log("surface: " + change);
        DrawnSea = new DrawnSea(surface, field);
        DrawnWater = new DrawnWater(DrawnSea);
        if (SurfaceShader.Attach(surface, field, capi))
        {
            clock = new SurfaceClock(this, sea, surface, field, capi);
            EyeUnderSurface.Attach(this, capi, DrawnWater, Config.Eye);
        }
        else Mod.Logger.Error("The sea surface is not drawn: its shaders failed to compile or vanilla's water shader changed; see the client log");
        VesselMotion.Attach(this, capi, DrawnSea, surface);
        FloatMotion.Attach(DrawnSea);
        RegisterSurfaceCommands(capi);
    }

    private static void RegisterSurfaceCommands(ICoreClientAPI capi)
    {
        var parsers = capi.ChatCommands.Parsers;
        capi.ChatCommands.Create("surface")
            .WithDescription("Rough Seas: the sea's surface as this client draws it")
            .BeginSubCommand("waves")
                .WithDescription("Draw the waves, or leave them out to compare")
                .WithArgs(parsers.Bool("draw"))
                .HandleWith(args => Drawn("waves", SurfaceShader.DrawWaves = (bool)args[0]))
            .EndSubCommand()
            .BeginSubCommand("foam")
                .WithDescription("Draw the foam, or leave it out to compare")
                .WithArgs(parsers.Bool("draw"))
                .HandleWith(args => Drawn("foam", SurfaceShader.DrawFoam = (bool)args[0]))
            .EndSubCommand()
            .BeginSubCommand("trace")
                .WithDescription("Log every vessel's pose on the waves every frame, for a number of seconds")
                .WithArgs(parsers.OptionalDouble("seconds", 3))
                .HandleWith(args =>
                {
                    VesselMotion.Trace((double)args[0]);
                    return TextCommandResult.Success($"Tracing vessels' poses for {args[0]} s, tagged 'surface trace' in the client's debug log");
                })
            .EndSubCommand()
            .BeginSubCommand("samplers")
                .WithDescription("List the texture units the engine numbers each shader program's samplers on")
                .HandleWith(_ => TextCommandResult.Success(SurfaceShader.Samplers()))
            .EndSubCommand()
            .BeginSubCommand("reload")
                .WithDescription("Read the surface's shader includes again from the mod's assets and recompile")
                .HandleWith(_ => SurfaceShader.Reload()
                    ? TextCommandResult.Success("Sea surface shaders reloaded")
                    : TextCommandResult.Error("Sea surface shaders failed to compile; see the client log"))
            .EndSubCommand();
    }

    public override void Dispose()
    {
        VesselDraught.Detach();
        HullDrift.Detach();
        if (api?.Side != EnumAppSide.Client) return;
        VesselMotion.Detach();
        FloatMotion.Detach();
        EyeUnderSurface.Detach();
        SurfaceShader.Detach();
        clock?.Dispose();
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

    private static TextCommandResult Drawn(string part, bool on) => TextCommandResult.Success($"Sea {part} {(on ? "drawn" : "left out")}");

    public void Log(Entity entity, string format, params object[] args) =>
        Log($"{entity.Code} {entity.EntityId} at {(int)entity.Pos.X} {(int)entity.Pos.Z}: {format}", args);

    public void Log(string format, params object[] args) =>
        Mod.Logger.Debug("[storms] " + string.Format(CultureInfo.InvariantCulture, format, args));
}
