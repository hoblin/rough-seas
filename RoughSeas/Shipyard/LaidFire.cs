using System;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Shipyard;

/// <summary>
/// A fire laid as vanilla lays a campfire: dry grass for its base, then firewood on the grass until the firepit is
/// built. Its state lives in the build's tree beside the rest of the build.
/// </summary>
public sealed class LaidFire(Func<ITreeAttribute> tree, Action changed)
{
    /// <summary>The firewood vanilla's <c>BlockFirepit</c> takes on its dry grass to finish a firepit.</summary>
    public const int FirepitFirewood = 4;

    private const string KindledKey = "kindled";
    private const string FirepitKey = "firepit";

    private ITreeAttribute Tree => tree();

    public bool Kindled => Tree?.GetBool(KindledKey) == true;

    public int OnFirepit => Tree?.GetInt(FirepitKey) ?? 0;

    public bool Laid => OnFirepit >= FirepitFirewood;

    public static void Kindle(ITreeAttribute tree) => tree.SetBool(KindledKey, true);

    public static void Lay(ITreeAttribute tree)
    {
        Kindle(tree);
        tree.SetInt(FirepitKey, FirepitFirewood);
    }

    public string? FirewoodRefusal() =>
        Laid ? "shipyard-laid-already"
        : !Kindled ? "shipyard-kindle-first"
        : null;

    public string? KindlingRefusal() => Kindled ? "shipyard-kindled-already" : null;

    public void Kindle()
    {
        Tree.SetBool(KindledKey, true);
        changed();
    }

    public void AddFirewood()
    {
        Tree.SetInt(FirepitKey, OnFirepit + 1);
        changed();
    }

    public void Spend()
    {
        Tree.SetBool(KindledKey, false);
        Tree.SetInt(FirepitKey, 0);
    }
}
