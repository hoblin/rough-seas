using Vintagestory.API.Client;

namespace RoughSeas.Soundscape;

public sealed class Swimmer(ICoreClientAPI capi, Splashes splashes)
{
    private readonly CrestWatch crests = new();

    public void Update()
    {
        if (capi.World.Player?.Entity is not { MountedOn: null } player || !(player.Swimming || player.FeetInLiquid))
        {
            crests.Forget();
            return;
        }
        var at = player.Pos.XYZ;
        if (crests.Watch(splashes.HeightAt(at)) is { } wave) splashes.Play(at, wave, "splash on the player");
    }
}
