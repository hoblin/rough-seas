using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Shipyard;

/// <summary>
/// A standing trunk with its bark stripped off: it stays a grown log, so the axe still fells the whole tree and the
/// leaves still hang on it; only its sides show bare wood. Vanilla's own debarked log is a placed log, which would do
/// neither.
/// </summary>
public static class DebarkedTrunk
{
    private const string Sides = "all";
    private const string BareSide = "inside-north";
    private static readonly string[] TreeKeys = ["treeFellingGroupCode", "treeFellingGroupSpreadIndex", "preventsDecay"];

    /// <summary>Server only, before the blocks go to clients.</summary>
    public static void Dress(IWorldAccessor world, Block debarked, Block grown, Wood wood)
    {
        debarked.Textures = new Dictionary<string, CompositeTexture>(grown.Textures);
        if (grown.Textures.TryGetValue(BareSide, out var bare)) debarked.Textures[Sides] = bare.Clone();

        var tree = new JObject { ["handbook"] = new JObject { ["exclude"] = true } };
        foreach (var key in TreeKeys)
        {
            if (grown.Attributes?[key] is { Exists: true } value) tree[key] = value.Token.DeepClone();
        }
        debarked.Attributes = new JsonObject(tree);

        var log = world.GetBlock(new AssetLocation(wood.Domain, $"debarkedlog-{wood.Species}-ud")) ?? world.GetBlock(wood.Log);
        if (log != null) debarked.Drops = [new BlockDropItemStack(new ItemStack(log))];
        debarked.CombustibleProps = grown.CombustibleProps;
        debarked.Resistance = grown.Resistance;
        debarked.RequiredMiningTier = grown.RequiredMiningTier;
    }
}
