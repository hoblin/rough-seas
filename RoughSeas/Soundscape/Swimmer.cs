using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RoughSeas.Soundscape;

public sealed class Swimmer(ICoreClientAPI capi, Splashes splashes)
{
    private readonly CrestWatch crests = new();
    private AssetLocation? lastSplash;

    public void Update()
    {
        if (capi.World.Player?.Entity is not { MountedOn: null } player || !(player.Swimming || player.FeetInLiquid))
        {
            crests.Forget();
            return;
        }
        var at = player.Pos.XYZ;
        if (crests.Watch(splashes.HeightAt(at)) is { } wave) lastSplash = splashes.Play(at, wave, "splash on the player", lastSplash);
    }
}
