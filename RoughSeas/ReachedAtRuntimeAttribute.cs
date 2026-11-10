using System;

namespace RoughSeas;

/// <summary>
/// Marks a method that reaches vanilla through a value known only when the game runs, which <c>bin/compat</c> can't
/// read from the assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ReachedAtRuntimeAttribute : Attribute
{
}
