using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace RoughSeas.Propulsion;

public sealed class Fatigue(RowerConfig config, VesselsModSystem vessels)
{
    private const double JoulesPerKilojoule = 1000;
    private const double ChargeSeconds = 10;

    private double seconds;
    private double satiety;

    public void Spend(Entity vessel, EntityPlayer rower, double watts, float deltaTime, string stroke)
    {
        if (rower.Player?.WorldData.CurrentGameMode is EnumGameMode.Creative or EnumGameMode.Spectator) return;

        var calendar = vessel.World.Calendar;
        var pace = calendar.SpeedOfTime * calendar.CalendarSpeedMul / config.VanillaPace;
        seconds += deltaTime;
        satiety += watts * deltaTime / JoulesPerKilojoule * config.SatietyPerKilojoule * pace;
        if (seconds < ChargeSeconds) return;

        rower.GetBehavior<EntityBehaviorHunger>()?.ConsumeSaturation((float)satiety);
        vessels.Log(vessel, "{0} spent {1:F1} satiety on {2:F0} s of {3}", rower.GetName(), satiety, seconds, stroke);
        (seconds, satiety) = (0, 0);
    }
}
