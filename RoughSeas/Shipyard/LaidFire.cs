using System;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Shipyard;

/// <summary>
/// A fire laid as vanilla lays a charcoal pit: a full pile of firewood, dry grass on it, and firewood on the grass
/// until the firepit is built. Its state lives in the build's tree beside the rest of the build.
/// </summary>
public sealed class LaidFire(Func<ITreeAttribute> tree, Action changed, int pileFirewood)
{
    /// <summary>The firewood vanilla's <c>BlockFirepit</c> takes on its dry grass to finish a firepit.</summary>
    public const int FirepitFirewood = 4;

    private const string PileKey = "firewood";
    private const string KindledKey = "kindled";
    private const string FirepitKey = "firepit";

    private ITreeAttribute Tree => tree();

    public int OnPile => Tree?.GetInt(PileKey) ?? 0;

    public int PileFirewood => pileFirewood;

    public bool Kindled => Tree?.GetBool(KindledKey) == true;

    public int OnFirepit => Tree?.GetInt(FirepitKey) ?? 0;

    public bool Laid => OnFirepit >= FirepitFirewood;

    public static void Lay(ITreeAttribute tree) => tree.SetInt(FirepitKey, FirepitFirewood);

    public string? FirewoodRefusal(bool piles) =>
        Laid ? "shipyard-laid-already"
        : OnPile < PileFirewood ? piles ? null : "shipyard-pile-first"
        : !Kindled ? "shipyard-kindle-first"
        : null;

    public string? KindlingRefusal() =>
        Kindled ? "shipyard-kindled-already"
        : OnPile < PileFirewood ? "shipyard-pile-first"
        : null;

    /// <returns>How many of <paramref name="offered"/> it takes: onto the pile until it is full, then one onto the firepit.</returns>
    public int Add(int offered)
    {
        int taken = OnPile < PileFirewood ? Math.Min(offered, PileFirewood - OnPile) : 1;
        if (OnPile < PileFirewood) Tree.SetInt(PileKey, OnPile + taken);
        else Tree.SetInt(FirepitKey, OnFirepit + 1);
        changed();
        return taken;
    }

    public void Kindle()
    {
        Tree.SetBool(KindledKey, true);
        changed();
    }

    public void Spend()
    {
        Tree.SetInt(PileKey, 0);
        Tree.SetBool(KindledKey, false);
        Tree.SetInt(FirepitKey, 0);
    }
}
