using System;

namespace RoughSeas;

/// <summary>
/// Marks code that matches a piece of text in a vanilla asset, such as an anchor in a shader. <c>bin/compat</c> checks
/// that the text is in the asset in each game version.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class ReliesOnTextAttribute : Attribute
{
    public ReliesOnTextAttribute(string asset, string text)
    {
    }
}
