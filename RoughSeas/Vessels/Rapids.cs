using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace RoughSeas.Vessels;

/// <summary>
/// A stub until rapids come from physics (#260): a flat strike a second on every hull in white water, sourced
/// nowhere, whatever she weighs or draws, and vessels exempted one by one by their <c>runsRapids</c> flag.
/// </summary>
public class Rapids
{
    public AssetLocation Block = null!;
    public double JoulesPerSecond;

    public bool Holds(Block fluid) => WildcardUtil.Match(Block, fluid.Code);
}
