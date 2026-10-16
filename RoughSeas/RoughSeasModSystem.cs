using Vintagestory.API.Common;

namespace RoughSeas;

public class RoughSeasModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        Mod.Logger.Notification($"Rough Seas {Mod.Info.Version} loaded on {api.Side}");
    }
}
