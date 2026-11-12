using System;
using Vintagestory.API.Common;

namespace RoughSeas.Person;

/// <summary>Which way a swimmer strokes, a unit vector: where they look while they try to move, up while they jump; none otherwise.</summary>
public readonly record struct SwimStroke(double X, double Y, double Z)
{
    public bool Stroking => X != 0 || Y != 0 || Z != 0;

    public static SwimStroke Of(EntityControls controls)
    {
        var fly = controls.TriesToMove ? Unit(controls.FlyVector.X, controls.FlyVector.Y, controls.FlyVector.Z) : default;
        return Unit(fly.X, fly.Y + (controls.Jump ? 1 : 0), fly.Z);
    }

    private static SwimStroke Unit(double x, double y, double z)
    {
        var length = Math.Sqrt(x * x + y * y + z * z);
        return length > 0 ? new SwimStroke(x / length, y / length, z / length) : default;
    }
}
