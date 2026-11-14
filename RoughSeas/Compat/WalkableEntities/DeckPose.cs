using System.Numerics;

namespace RoughSeas.Compat.WalkableEntities;

public readonly record struct DeckPose(double X, double Y, double Z, Quaternion Rotation)
{
    /// <summary>
    /// This pose as the renderer draws the boat: turned on by its <c>xangle</c>, <c>yangle</c> and <c>zangle</c> about
    /// the middle of its selection box, as <c>EntityShapeRenderer.loadModelMatrix</c> turns them after the boat's own
    /// angles, and lifted.
    /// </summary>
    public DeckPose Drawn(float pivotY, float xangle, float yangle, float zangle, double lift)
    {
        var drawn = Rotation
            * Quaternion.CreateFromAxisAngle(Vector3.UnitX, xangle)
            * Quaternion.CreateFromAxisAngle(Vector3.UnitY, yangle)
            * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, zangle);
        var pivot = new Vector3(0, pivotY, 0);
        var shift = Vector3.Transform(pivot, Rotation) - Vector3.Transform(pivot, drawn);
        return new DeckPose(X + shift.X, Y + shift.Y + lift, Z + shift.Z, drawn);
    }

    public Vector3 ToLocal(double x, double y, double z) =>
        Vector3.Transform(new Vector3((float)(x - X), (float)(y - Y), (float)(z - Z)), Quaternion.Inverse(Rotation));

    public (double X, double Y, double Z) ToWorld(Vector3 local)
    {
        var turned = Vector3.Transform(local, Rotation);
        return (X + turned.X, Y + turned.Y, Z + turned.Z);
    }
}
