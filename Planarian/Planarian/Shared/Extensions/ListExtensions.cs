namespace Planarian.Shared.Extensions;

public static class ListExtensions
{
    public static IEnumerable<List<T>> Chunk<T>(this IEnumerable<T> source, int chunkSize)
    {
        var list = new List<T>(chunkSize);
        foreach (var element in source)
        {
            list.Add(element);
            if (list.Count == chunkSize)
            {
                yield return list;
                list = new List<T>(chunkSize);
            }
        }

        if (list.Count > 0) yield return list;
    }
}