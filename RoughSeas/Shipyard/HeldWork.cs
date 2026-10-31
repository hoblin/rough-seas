using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

/// <summary>
/// Work done by holding the use button for its time: the bar fills on the holder's screen, and the work is done once
/// the button has been held long enough.
/// </summary>
public sealed class HeldWork
{
    private const float Slack = 0.1f;

    private IProgressBar? bar;

    public void Start(EntityAgent byEntity)
    {
        if (byEntity.Api is not ICoreClientAPI capi) return;

        Stop(byEntity);
        bar = capi.ModLoader.GetModSystem<ModSystemProgressBar>().AddProgressbar();
    }

    /// <returns>Whether to keep holding.</returns>
    public bool Step(float secondsUsed, double seconds)
    {
        if (bar != null) bar.Progress = (float)(secondsUsed / seconds);
        return secondsUsed < seconds;
    }

    /// <returns>Whether the button was held long enough for the work to be done.</returns>
    public bool Stop(EntityAgent byEntity, float secondsUsed = 0, double seconds = double.PositiveInfinity)
    {
        if (bar != null && byEntity.Api is ICoreClientAPI capi) capi.ModLoader.GetModSystem<ModSystemProgressBar>().RemoveProgressbar(bar);
        bar = null;
        return secondsUsed >= seconds - Slack;
    }
}
