using System;

namespace RoughSeas;

/// <summary>
/// Marks code that depends on how a vanilla method behaves, not just on it existing. <c>bin/compat</c> compares the
/// named method's own body, not what it calls, across game versions, and lists a real difference for reading beside
/// the behaviour named here. All overloads of the name are compared.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
public sealed class ReliesOnAttribute : Attribute
{
    public ReliesOnAttribute(string vanillaType, string method, string behaviour)
    {
    }
}
