using System.Globalization;
using Vintagestory.API.Common;

namespace RoughSeas.Compat;

/// <summary>
/// Rough Seas beside another mod whose code it reaches into: loaded with that mod, and acting only on a version listed
/// as tested in <c>config/compat/&lt;modid&gt;.json</c>.
/// </summary>
public abstract class OtherModCompat : ModSystem
{
    protected abstract string ModId { get; }

    protected abstract string LogTag { get; }

    protected AssetLocation ConfigAsset => new($"roughseas:config/compat/{ModId}.json");

    protected bool Tested { get; private set; }

    public override bool ShouldLoad(ICoreAPI api) => api.ModLoader.IsModEnabled(ModId);

    public override void AssetsLoaded(ICoreAPI api)
    {
        var tested = api.Assets.Get<TestedConfig>(ConfigAsset).Tested;
        var info = api.ModLoader.GetMod(ModId).Info;
        Tested = tested.Covers(info.Version);
        if (Tested) Meet(api, info.Name, info.Version);
        else Mod.Logger.Warning("{0} {1} is untested (tested: {2}); Rough Seas leaves it as it is", info.Name, info.Version, tested);
    }

    protected abstract void Meet(ICoreAPI api, string name, string version);

    public void Log(string format, params object[] args) =>
        Mod.Logger.Debug($"[{LogTag}] " + string.Format(CultureInfo.InvariantCulture, format, args));
}
