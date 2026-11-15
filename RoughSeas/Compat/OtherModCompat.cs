using System;
using System.Globalization;
using Vintagestory.API.Common;

namespace RoughSeas.Compat;

/// <summary>
/// Rough Seas beside another mod whose code it reaches into: loaded with that mod, and acting only when the mod has every
/// member we reach. A module finds all of them in <see cref="Meet"/> before it changes anything, and throws a
/// <see cref="MissingMemberException"/> for the first one missing.
/// </summary>
public abstract class OtherModCompat : ModSystem
{
    protected abstract string ModId { get; }

    protected abstract string LogTag { get; }

    protected AssetLocation ConfigAsset => new($"roughseas:config/compat/{ModId}.json");

    protected bool Met { get; private set; }

    public override bool ShouldLoad(ICoreAPI api) => api.ModLoader.IsModEnabled(ModId);

    public override void AssetsLoaded(ICoreAPI api)
    {
        var info = api.ModLoader.GetMod(ModId).Info;
        try
        {
            Meet(api, info.Name, info.Version);
            Met = true;
        }
        catch (MissingMemberException missing)
        {
            Mod.Logger.Warning("{0} {1} lacks what Rough Seas reaches in it: {2}; Rough Seas leaves it as it is", info.Name, info.Version, missing.Message);
        }
    }

    protected abstract void Meet(ICoreAPI api, string name, string version);

    public void Log(string format, params object[] args) =>
        Mod.Logger.Debug($"[{LogTag}] " + string.Format(CultureInfo.InvariantCulture, format, args));
}
