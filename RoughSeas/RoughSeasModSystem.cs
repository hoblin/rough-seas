using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace RoughSeas;

public class RoughSeasModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        Mod.Logger.Notification($"Rough Seas {Mod.Info.Version} loaded on {api.Side}: {Lang.Get("roughseas:hello")}");
    }
}
