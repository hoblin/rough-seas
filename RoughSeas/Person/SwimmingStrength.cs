using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace RoughSeas.Person;

/// <summary>
/// How hard a player's arms pull over a trained swimmer's: the world's own setting, the game's walk speed over its
/// Normal and the player's own move speed, the two that speed the base game's swimmer.
/// </summary>
public static class SwimmingStrength
{
    public const string Code = "swimmingStrength";
    private const double NormalWalkSpeed = 1.5;

    [ReliesOn("Vintagestory.GameContent.SurvivalCoreSystem", "applyConfig", "writes the world's walk speed setting into GlobalConstants.BaseMoveSpeed on the server and, from the packet a joining player gets, on the client")]
    public static double Of(Entity entity) =>
        entity.World.Config.GetDecimal(Code, 1) * GlobalConstants.BaseMoveSpeed / NormalWalkSpeed
        * (entity is EntityAgent agent ? agent.Controls.MovespeedMultiplier : 1);
}
