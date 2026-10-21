using System;
using System.Collections.Generic;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

public class HullContacts(EntityPartitioning partitioning)
{
    public List<Contact> Around(Entity vessel, HullShape shape, bool withSteered)
    {
        var pos = vessel.Pos;
        var own = Footprint.Of(shape, pos.X, pos.Z, pos.Yaw);
        var contacts = new List<Contact>();
        partitioning.WalkEntities(pos.XYZ, shape.HalfLength + Math.Abs(shape.CentreAlong) + partitioning.LargestTouchDistance, other =>
        {
            if (other != vessel && other.Alive && other.Pos.Dimension == pos.Dimension && IHull.Of(other) is { } hull &&
                (withSteered || !hull.Steered) &&
                Level(pos.Y, shape, other.Pos.Y, hull.Shape) &&
                Footprint.Of(hull.Shape, other.Pos.X, other.Pos.Z, other.Pos.Yaw) is var theirs && own.Touches(theirs))
            {
                var closing = pos.Motion - other.Pos.Motion;
                contacts.Add(new Contact(other.EntityId, theirs.X - own.X, theirs.Z - own.Z, closing.X, closing.Z, hull.Aboard.Impact));
            }
            return true;
        }, EnumEntitySearchType.Creatures);
        return contacts;
    }

    public static bool Level(double y, HullShape shape, double theirY, HullShape theirs) =>
        y + shape.Keel <= theirY + theirs.Keel + theirs.Depth && theirY + theirs.Keel <= y + shape.Keel + shape.Depth;
}
