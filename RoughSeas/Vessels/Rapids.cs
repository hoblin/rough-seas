using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace RoughSeas.Vessels;

public class Rapids
{
    public AssetLocation Block = null!;
    public double JoulesPerSecond;

    public bool Holds(Block fluid) => WildcardUtil.Match(Block, fluid.Code);
}
