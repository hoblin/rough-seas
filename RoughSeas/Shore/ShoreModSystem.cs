using System.Globalization;
using RoughSeas.SeaAndWeather;
using RoughSeas.Water;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RoughSeas.Shore;

public class ShoreModSystem : ModSystem
{
    private static readonly AssetLocation ConfigAsset = new("roughseas:config/shore.json");

    internal static Groundwater? Water { get; private set; }

    public ShoreConfig Config { get; private set; } = new();

    public override double ExecuteOrder() => 1.1;

    private EnumAppSide side;

    public override void Start(ICoreAPI api)
    {
        side = api.Side;
        api.RegisterBlockBehaviorClass(BlockBehaviorGroundwater.Code, typeof(BlockBehaviorGroundwater));
        VanillaShore.Attach();
    }

    public override void AssetsLoaded(ICoreAPI api) => Config = api.Assets.Get<ShoreConfig>(ConfigAsset);

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        Water = new Groundwater(sapi, sapi.ModLoader.GetModSystem<SeaWeatherSystem>(), Config, Log);
        Water.Start();
        sapi.ChatCommands.Create("shore")
            .WithDescription("The water under the shore")
            .RequiresPrivilege(Privilege.controlserver)
            .BeginSubCommand("here")
                .WithDescription("The ground under the player at still-water level, and how far the nearest shore's sea runs up it")
                .RequiresPlayer()
                .HandleWith(args =>
                {
                    var at = args.Caller.Entity.Pos.AsBlockPos;
                    at.Y = sapi.World.SeaLevel - 1;
                    return TextCommandResult.Success(Water.Describe(at));
                })
            .EndSubCommand()
            .BeginSubCommand("budget")
                .WithDescription("Time the shore's water has taken on the server since the last report")
                .HandleWith(_ => TextCommandResult.Success(Water.Budget.Report()))
            .EndSubCommand();
    }

    public override void Dispose()
    {
        if (side != EnumAppSide.Server) return;
        VanillaShore.Detach();
        Water = null;
    }

    private void Log(string format, object[] args) =>
        Mod.Logger.Debug("[shore] " + string.Format(CultureInfo.InvariantCulture, format, args));
}
