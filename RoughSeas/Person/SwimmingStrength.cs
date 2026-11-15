using System.Linq;
using Func = System.Func<Vintagestory.API.Common.Entities.Entity, double>;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace RoughSeas.Person;

/// <summary>
/// How hard a player's arms pull over a trained swimmer's: the world's own setting, the game's walk speed over its
/// Normal, the player's own move speed and each factor added by a module for another mod. The base game's swimmer goes
/// as many times faster by these; ours pulls as many times harder, against the same drag.
/// </summary>
public static class SwimmingStrength
{
    public const string Code = "swimmingStrength";
    private const double NormalWalkSpeed = 1.5;

    private static readonly object gate = new();
    private static Func[] factors = [];

    [ReliesOn("Vintagestory.GameContent.SurvivalCoreSystem", "applyConfig", "writes the world's walk speed setting into GlobalConstants.BaseMoveSpeed on the server and, from the packet a joining player gets, on the client")]
    public static double Of(Entity entity) =>
        entity.World.Config.GetDecimal(Code, 1) * GlobalConstants.BaseMoveSpeed / NormalWalkSpeed
        * (entity is EntityAgent agent ? agent.Controls.MovespeedMultiplier : 1) * Factor(entity);

    public static double Factor(Entity entity) => factors.Aggregate(1.0, (strength, factor) => strength * factor(entity));

    public static void Add(Func factor)
    {
        lock (gate) factors = [.. factors, factor];
    }

    public static void Remove(Func factor)
    {
        lock (gate) factors = [.. factors.Where(other => other != factor)];
    }
}
