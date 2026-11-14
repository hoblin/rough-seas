using System.Globalization;
using Vintagestory.API.Common;

namespace RoughSeas.Compat;

public abstract class OtherModCompat : ModSystem
{
    protected abstract string ModId { get; }

    protected abstract string ModName { get; }

    protected abstract string LogTag { get; }

    protected AssetLocation ConfigAsset => new($"roughseas:config/compat/{ModId}.json");

    protected bool Tested { get; private set; }

    public override bool ShouldLoad(ICoreAPI api) => api.ModLoader.IsModEnabled(ModId);

    public override void AssetsLoaded(ICoreAPI api)
    {
        var tested = api.Assets.Get<TestedConfig>(ConfigAsset).Tested;
        var version = api.ModLoader.GetMod(ModId).Info.Version;
        Tested = tested.Covers(version);
        if (Tested) Meet(api, version);
        else Mod.Logger.Warning("{0} {1} is untested (tested: {2}); Rough Seas leaves it as it is", ModName, version, tested);
    }

    protected abstract void Meet(ICoreAPI api, string version);

    public void Log(string format, params object[] args) =>
        Mod.Logger.Debug($"[{LogTag}] " + string.Format(CultureInfo.InvariantCulture, format, args));
}
