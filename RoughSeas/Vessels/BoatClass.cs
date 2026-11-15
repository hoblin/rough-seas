using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

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
    private readonly Action<Entity, float> setSpeedMultiplier;

    public BoatClass(Type type)
    {
        Type = type;
        mountAngle = Ref<Vec3f>(type, "mountAngle");
        forwardSpeed = Ref<double>(type, "ForwardSpeed");
        mountAnimations = Ref<Dictionary<string, string>>(type, "MountAnimations");
        setSpeedMultiplier = Setter(type, AccessTools.PropertySetter(type, "SpeedMultiplier") is { } setter && setter.GetParameters()[0].ParameterType == typeof(float)
            ? setter
            : throw new MissingMemberException(type.FullName, "SpeedMultiplier of Single"));
        OnRenderFrame = Declared(type, "OnRenderFrame");
        UpdateMotion = Declared(type, "updateBoatAngleAndMotion");
        SwimmingOffsetY = AccessTools.DeclaredPropertyGetter(type, "SwimmingOffsetY") is { } getter
            && getter.ReturnType == AccessTools.PropertyGetter(typeof(EntityBoat), "SwimmingOffsetY").ReturnType
            ? getter
            : throw new MissingMemberException(type.FullName, "SwimmingOffsetY as vanilla's boat has it");
    }

    public Type Type { get; }

    public MethodInfo OnRenderFrame { get; }

    public MethodInfo UpdateMotion { get; }

    public MethodInfo SwimmingOffsetY { get; }

    public Vec3f MountAngle(Entity boat) => mountAngle(boat);

    public double ForwardSpeed(Entity boat) => forwardSpeed(boat);

    public Dictionary<string, string> MountAnimations(Entity boat) => mountAnimations(boat);

    public void SetSpeedMultiplier(Entity boat, float multiplier) => setSpeedMultiplier(boat, multiplier);

    private static Action<Entity, float> Setter(Type type, MethodInfo setter)
    {
        var boat = Expression.Parameter(typeof(Entity));
        var value = Expression.Parameter(typeof(float));
        return Expression.Lambda<Action<Entity, float>>(Expression.Call(Expression.Convert(boat, type), setter, value), boat, value).Compile();
    }

    private static AccessTools.FieldRef<object, F> Ref<F>(Type type, string name) =>
        AccessTools.Field(type, name)?.FieldType == typeof(F) ? AccessTools.FieldRefAccess<F>(type, name) : throw new MissingFieldException(type.FullName, $"{name} of {typeof(F).Name}");

    private static MethodInfo Declared(Type type, string name) =>
        AccessTools.DeclaredMethod(type, name, [.. AccessTools.Method(typeof(EntityBoat), name).GetParameters().Select(parameter => parameter.ParameterType)])
        ?? throw new MissingMethodException(type.FullName, $"{name} as vanilla's boat has it");
}
