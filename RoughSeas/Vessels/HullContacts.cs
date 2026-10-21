using System;
using System.Collections.Generic;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

public class HullContacts(EntityPartitioning partitioning)
{
    public List<Contact> Around(Entity vessel, HullShape shape)
    {
        var pos = vessel.Pos;
        var own = Footprint.Of(shape, pos.X, pos.Z, pos.Yaw);
        var contacts = new List<Contact>();
        partitioning.WalkEntities(pos.XYZ, shape.HalfLength + Math.Abs(shape.CentreAlong) + partitioning.LargestTouchDistance, other =>
        {
            if (other != vessel && other.Alive && other.Pos.Dimension == pos.Dimension &&
                other.GetBehavior<EntityBehaviorVesselIntegrity>() is { } hull &&
                Footprint.Of(hull.Shape, other.Pos.X, other.Pos.Z, other.Pos.Yaw) is var theirs && own.Touches(theirs))
            {
                var closing = pos.Motion - other.Pos.Motion;
                contacts.Add(new Contact(other.EntityId, theirs.X - own.X, theirs.Z - own.Z, closing.X, closing.Z));
            }
            return true;
        }, EnumEntitySearchType.Creatures);
        return contacts;
    }
}
