using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

/// <summary>
/// Work done by holding the use button for its time: the bar fills on the holder's screen, and the work is done once
/// the button has been held long enough. A hold ends once, however often the game reports its end: the server calls
/// a held item's stop twice when it finishes the use while catching up the client's stop.
/// </summary>
public sealed class HeldWork
{
    private const float Slack = 0.1f;

    private readonly HashSet<long> holding = [];
    private IProgressBar? bar;

    public void Start(EntityAgent byEntity)
    {
        holding.Add(byEntity.EntityId);
        if (byEntity.Api is not ICoreClientAPI capi) return;

        RemoveBar(capi);
        bar = capi.ModLoader.GetModSystem<ModSystemProgressBar>().AddProgressbar();
    }

    /// <returns>Whether to keep holding.</returns>
    public bool Step(float secondsUsed, double seconds)
    {
        if (bar != null) bar.Progress = (float)(secondsUsed / seconds);
        return secondsUsed < seconds;
    }

    /// <returns>Whether this ends a hold long enough for the work to be done.</returns>
    public bool Stop(EntityAgent byEntity, float secondsUsed = 0, double seconds = double.PositiveInfinity)
    {
        if (byEntity.Api is ICoreClientAPI capi) RemoveBar(capi);
        return holding.Remove(byEntity.EntityId) && secondsUsed >= seconds - Slack;
    }

    private void RemoveBar(ICoreClientAPI capi)
    {
        if (bar != null) capi.ModLoader.GetModSystem<ModSystemProgressBar>().RemoveProgressbar(bar);
        bar = null;
    }
}
