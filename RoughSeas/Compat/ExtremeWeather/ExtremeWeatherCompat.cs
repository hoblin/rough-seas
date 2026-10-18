using System.Globalization;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using RoughSeas.SeaAndWeather;

namespace RoughSeas.Compat.ExtremeWeather;

public class ExtremeWeatherCompat : ModSystem
{
    public const string ModId = "extremeweather";

    private static readonly AssetLocation ConfigAsset = new("roughseas:config/compat/extremeweather.json");

    private bool tested;

    public CompatConfig Config { get; private set; } = null!;

    public override bool ShouldLoad(ICoreAPI api) => api.ModLoader.IsModEnabled(ModId);

    public override void AssetsLoaded(ICoreAPI api)
    {
        Config = api.Assets.Get<CompatConfig>(ConfigAsset);
        var version = api.ModLoader.GetMod(ModId).Info.Version;
        tested = Config.Tested.Covers(version);
        if (!tested)
        {
            Mod.Logger.Warning("Extreme Weathers {0} is untested (tested: {1}); Rough Seas leaves it as it is", version, Config.Tested);
            return;
        }

        WindCap.Attach();
        Log("Extreme Weathers {0}: its wind cap holds only during its own events", version);
    }

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        if (!tested) return;

        Settle();
        new HurricaneTornadoes(this, sapi.ModLoader.GetModSystem<SeaWeatherSystem>(), sapi).Start();
        sapi.ChatCommands.Create("roughseas")
            .WithDescription("Rough Seas beside other mods")
            .RequiresPrivilege(Privilege.controlserver)
            .BeginSubCommand(ModId)
                .WithDescription("Extreme Weathers' settings Rough Seas holds, as they stand now")
                .HandleWith(_ => TextCommandResult.Success(string.Join(", ", Config.Settings.Keys.Select(name => $"{name} {ExtremeWeatherMod.Get(name)}"))))
            .EndSubCommand();
    }

    public override void StartClientSide(ICoreClientAPI capi)
    {
        if (tested) Settle();
    }

    private void Settle()
    {
        ExtremeWeatherMod.Set(Config.Settings);
        Log("Extreme Weathers set: {0}", string.Join(", ", Config.Settings));
    }

    public override void Dispose()
    {
        if (tested) WindCap.Detach();
    }

    public void Log(string format, params object[] args) =>
        Mod.Logger.Debug("[ew] " + string.Format(CultureInfo.InvariantCulture, format, args));
}
