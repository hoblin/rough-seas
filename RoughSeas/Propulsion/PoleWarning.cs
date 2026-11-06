using System.Globalization;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace RoughSeas.Propulsion;

public sealed class PoleWarning
{
    private const string Code = "roughseas-pole-too-deep";
    private const long ShownMilliseconds = 5000;

    private long? shownAt;

    public void Tell(Entity rower, Pole? unreaching, bool asking)
    {
        if (rower.Api is not ICoreClientAPI capi || rower != capi.World.Player?.Entity) return;
        if (unreaching == null || !asking)
        {
            shownAt = null;
            return;
        }

        var now = capi.InWorldEllapsedMilliseconds;
        if (shownAt is { } at && now - at < ShownMilliseconds) return;
        shownAt = now;
        capi.TriggerIngameError(this, Code, Lang.Get("roughseas:propulsion-pole-too-deep", unreaching.Metres.ToString("0", CultureInfo.InvariantCulture)));
    }
}
