using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace RoughSeas.Vessels;

/// <summary>
/// A class of entity that is a boat as vanilla's is: <c>EntityBoat</c>, or another mod's copy of it with the same
/// members, found by name, so a class we can't compile against is a boat to us all the same.
/// </summary>
public sealed class BoatClass
{
    private readonly AccessTools.FieldRef<object, Vec3f> mountAngle;
    private readonly AccessTools.FieldRef<object, double> forwardSpeed;
    private readonly AccessTools.FieldRef<object, Dictionary<string, string>> mountAnimations;
    private readonly PropertyInfo speedMultiplier;

    public BoatClass(Type type)
    {
        Type = type;
        mountAngle = AccessTools.FieldRefAccess<Vec3f>(type, "mountAngle");
        forwardSpeed = AccessTools.FieldRefAccess<double>(type, "ForwardSpeed");
        mountAnimations = AccessTools.FieldRefAccess<Dictionary<string, string>>(type, "MountAnimations");
        speedMultiplier = AccessTools.Property(type, "SpeedMultiplier") ?? throw new MissingMemberException(type.FullName, "SpeedMultiplier");
        OnRenderFrame = Declared(type, "OnRenderFrame");
        UpdateMotion = Declared(type, "updateBoatAngleAndMotion");
        SwimmingOffsetY = AccessTools.DeclaredPropertyGetter(type, "SwimmingOffsetY") ?? throw new MissingMemberException(type.FullName, "SwimmingOffsetY");
    }

    public Type Type { get; }

    public MethodInfo OnRenderFrame { get; }

    public MethodInfo UpdateMotion { get; }

    public MethodInfo SwimmingOffsetY { get; }

    public Vec3f MountAngle(Entity boat) => mountAngle(boat);

    public double ForwardSpeed(Entity boat) => forwardSpeed(boat);

    public Dictionary<string, string> MountAnimations(Entity boat) => mountAnimations(boat);

    public void SetSpeedMultiplier(Entity boat, float multiplier) => speedMultiplier.SetValue(boat, multiplier);

    private static MethodInfo Declared(Type type, string name) =>
        AccessTools.DeclaredMethod(type, name) ?? throw new MissingMethodException(type.FullName, name);
}
