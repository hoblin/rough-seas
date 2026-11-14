using Vintagestory.API.Common;

namespace RoughSeas.Compat.WalkableEntities;

public class WalkableEntitiesCompat : OtherModCompat
{
    protected override string ModId => "walkableentities";

    protected override string ModName => "Walkable Entities";

    protected override string LogTag => "we";

    protected override void Meet(ICoreAPI api, string version)
    {
        WalkableDecks.Attach();
        Log("Walkable Entities {0}: a deck we pose carries whoever stands on it with the swell", version);
    }

    public override void Dispose()
    {
        if (Tested) WalkableDecks.Detach();
    }
}
