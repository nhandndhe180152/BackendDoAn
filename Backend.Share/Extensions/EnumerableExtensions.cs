using System;

namespace Backend.Share.Extensions;

public static class EnumerableExtensions
{
    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? enumerable) =>
        enumerable == null || !enumerable.Any();

    public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> items, Func<T, TKey> keySelector)
    {
        return items.GroupBy(keySelector).Select(g => g.First());
    }
}
