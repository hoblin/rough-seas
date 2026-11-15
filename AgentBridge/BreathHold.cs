using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace AgentBridge;

/// <summary>
/// Keeps every player's lungs full while <see cref="Breathing"/> is off, so a swimmer left in the water through a test
/// doesn't drown. Vanilla's breath still runs and shows; only the oxygen it spends is given back.
/// </summary>
public sealed class BreathHold
{
    private const int TickMilliseconds = 250;

    public BreathHold(ICoreServerAPI sapi, bool breathing)
    {
        Breathing = breathing;
        sapi.Event.RegisterGameTickListener(_ =>
        {
            if (Breathing) return;
            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.GetBehavior<EntityBehaviorBreathe>() is { } lungs) lungs.Oxygen = lungs.MaxOxygen;
            }
        }, TickMilliseconds);
    }

    public bool Breathing { get; set; }
}
