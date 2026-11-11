using Vintagestory.API.Common.Entities;

namespace RoughSeas.Person;

/// <summary>
/// What a player weighs and displaces. Every system that weighs a player asks here,
/// through <c>api.ModLoader.GetModSystem&lt;PersonModSystem&gt;().Person</c>, on both sides.
/// </summary>
public interface IPerson
{
    /// <summary>The one reference body every player has: its mass, and what it displaces by height.</summary>
    Body Body { get; }

    /// <summary>What an entity carries now: a player's armour and the tools and weapons in their hotbar; nothing for any other entity.</summary>
    Carried CarriedBy(Entity entity);
}

/// <summary>What is carried: its mass, and the water it displaces wholly under.</summary>
public readonly record struct Carried(double Kg, double Litres);
