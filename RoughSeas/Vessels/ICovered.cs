namespace RoughSeas.Vessels;

/// <summary>A hull whose cover is sewn on her as she is built.</summary>
public interface ICovered
{
    /// <summary>The hides on her so far; null leaves her cover to her hull's designed <c>cover</c>.</summary>
    HideCover? Cover { get; }
}
