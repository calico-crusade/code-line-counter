namespace CodeLineCounter.Core;

/// <summary>
/// Represents a cachable item
/// </summary>
/// <typeparam name="T">The type of cachable item</typeparam>
public class CacheItem<T>
{
    /// <summary>
    /// How to resolve the current value of the item
    /// </summary>
    private readonly Func<T> _resolver;

    /// <summary>
    /// The current value of the item (or null if not resolved)
    /// </summary>
    private T? _value;

    /// <summary>
    /// Represents a cachable item
    /// </summary>
    /// <param name="resolver">How to resolve the current value of the item</param>
    public CacheItem(Func<T> resolver)
    {
        _resolver = resolver;
    }

    /// <summary>
    /// Converts the given cache item to the underlying value
    /// </summary>
    /// <param name="item">The cache item to resolve the value for</param>
    public static implicit operator T(CacheItem<T> item)
    {
        return item._value ??= item._resolver();
    }
}
