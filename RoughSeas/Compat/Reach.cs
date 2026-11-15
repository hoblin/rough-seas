using System;
using System.Linq;
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
        AccessTools.Method(type, name, parameters) ?? throw new MissingMethodException(type.FullName, $"{name}({string.Join(", ", parameters.Select(parameter => parameter.Name))})");

    public static MethodInfo Returning(this MethodInfo method, Type type) =>
        method.ReturnType == type ? method : throw new MissingMethodException(method.DeclaringType?.FullName, $"{method.Name} returning {type.Name}");
}
