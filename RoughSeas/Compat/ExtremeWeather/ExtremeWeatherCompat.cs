using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using RoughSeas.SeaAndWeather;

namespace RoughSeas.Compat.ExtremeWeather;

public class ExtremeWeatherCompat : OtherModCompat
{
    public const string Id = "extremeweather";

    protected override string ModId => Id;

    protected override string LogTag => "ew";

    public CompatConfig Config { get; private set; } = null!;

    public ExtremeWeatherMod Ew { get; private set; } = null!;

    protected override void Meet(ICoreAPI api, string name, string version)
    {
        Config = api.Assets.Get<CompatConfig>(ConfigAsset);
        Ew = new ExtremeWeatherMod(Config.Settings.Keys);
        WindCap.Attach(Ew.ApplyEventWind);
        Log("{0} {1}: its wind cap holds only during its own events", name, version);
    }

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        if (!Met) return;

        Settle();
        new HurricaneTornadoes(this, sapi.ModLoader.GetModSystem<SeaWeatherSystem>(), sapi).Start();
        sapi.ChatCommands.Create("roughseas")
            .WithDescription("Rough Seas beside other mods")
            .RequiresPrivilege(Privilege.controlserver)
            .BeginSubCommand(Id)
                .WithDescription("Extreme Weathers' settings Rough Seas holds, as they stand now")
                .HandleWith(_ => TextCommandResult.Success(string.Join(", ", Config.Settings.Keys.Select(name => $"{name} {Ew.Get(name)}"))))
            .EndSubCommand();
    }

    public override void StartClientSide(ICoreClientAPI capi)
    {
        if (Met) Settle();
    }

    private void Settle()
    {
        Ew.Set(Config.Settings);
        Log("Extreme Weathers set: {0}", string.Join(", ", Config.Settings));
    }

    public override void Dispose()
    {
        if (Met) WindCap.Detach();
    }
}
