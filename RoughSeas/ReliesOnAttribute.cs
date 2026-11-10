using System;

namespace RoughSeas;

/// <summary>
/// Marks code that depends on how a vanilla method behaves, not just on it existing. <c>bin/compat</c> compares the
/// named method across game versions and lists a real difference for reading, beside <paramref name="behaviour"/>.
/// All overloads of the name are compared.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
public sealed class ReliesOnAttribute : Attribute
{
    public ReliesOnAttribute(string vanillaType, string method, string behaviour)
    {
    }
}

/// <summary>
/// Marks a method that reaches vanilla through a value known only when the game runs, which <c>bin/compat</c> can't
/// read from the assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ReachedAtRuntimeAttribute : Attribute
{
}
