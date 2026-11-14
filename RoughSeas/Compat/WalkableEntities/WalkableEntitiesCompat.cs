using Vintagestory.API.Common;

namespace RoughSeas.Compat.WalkableEntities;

public class WalkableEntitiesCompat : OtherModCompat
{
    protected override string ModId => "walkableentities";

    protected override string LogTag => "we";

    protected override void Meet(ICoreAPI api, string name, string version)
    {
        WalkableDecks.Attach();
        Log("{0} {1}: a deck we pose carries whoever stands on it with the swell", name, version);
    }

    public override void Dispose()
    {
        if (Tested) WalkableDecks.Detach();
    }
}
