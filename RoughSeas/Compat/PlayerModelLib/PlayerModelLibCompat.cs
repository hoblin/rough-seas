using System;
using RoughSeas.Person;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace RoughSeas.Compat.PlayerModelLib;

public class PlayerModelLibCompat : OtherModCompat
{
    private const string SwimSpeed = "swimSpeed";
    private const float MaxSwimSpeed = 10;

    private readonly System.Func<Entity, double> swimmer = entity => Math.Clamp(entity.Stats.GetBlended(SwimSpeed), 0, MaxSwimSpeed);

    protected override string ModId => "playermodellib";

    protected override string LogTag => "pml";

    protected override void Meet(ICoreAPI api, string name, string version)
    {
        SwimmingStrength.Add(swimmer);
        Log("{0} {1}: a player's {2} stat multiplies the arms' pull", name, version, SwimSpeed);
    }

    public override void Dispose()
    {
        if (Met) SwimmingStrength.Remove(swimmer);
    }
}
