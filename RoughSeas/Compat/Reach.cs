using System;
using System.Reflection;
using HarmonyLib;

namespace RoughSeas.Compat;

/// <summary>Another mod's type or member by name, or the <see cref="MissingMemberException"/> that says it is gone.</summary>
public static class Reach
{
    public static Type Type(string name) =>
        AccessTools.TypeByName(name) ?? throw new MissingMemberException($"type {name}");

    public static FieldInfo Field(Type type, string name, Type? of = null) =>
        AccessTools.Field(type, name) is { } field && (of == null || field.FieldType == of)
            ? field
            : throw new MissingFieldException(type.FullName, of == null ? name : $"{name} of {of.Name}");

    public static MethodInfo Method(Type type, string name, params Type[] parameters) =>
        AccessTools.Method(type, name, parameters.Length > 0 ? parameters : null) ?? throw new MissingMethodException(type.FullName, name);
}
