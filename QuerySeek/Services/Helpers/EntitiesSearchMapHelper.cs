using QuerySeek.Models;

namespace QuerySeek.Services.Helpers;

public static class EntitiesSearchMapHelper
{
    public static WordMatchMeta[]? GetMatchesByWord(
        this KeyValuePair<byte, Dictionary<Key, WordMatchMeta[]>>[][] searchMap,
        int wordId,
        byte entityType)
    {
        KeyValuePair<byte, Dictionary<Key, WordMatchMeta[]>>[] wordMatches = searchMap[wordId];

        //Типы в бандле упорядочены. Используем бинарный поиск.
        int index = BinarySearch(wordMatches, entityType);
        if (index == -1) return null;

        if (wordMatches[index].Value.TryGetValue(Key.Default, out WordMatchMeta[]? matches))
        {
            return matches;
        }

        return null;
    }

    public static IEnumerable<WordMatchMeta> GetMatchesByWordAndContainers(
        this KeyValuePair<byte, Dictionary<Key, WordMatchMeta[]>>[][] searchMap,
        int wordId,
        byte entityType,
        Key[] containerKeys)
    {
        KeyValuePair<byte, Dictionary<Key, WordMatchMeta[]>>[] wordMatches = searchMap[wordId];

        //Типы в бандле упорядочены. Используем бинарный поиск.
        int index = BinarySearch(wordMatches, entityType);
        if (index == -1) yield break;

        Dictionary<Key, WordMatchMeta[]> matchesBundleByContainers = wordMatches[index].Value;

        foreach (Key containerKey in containerKeys)
        {
            if (!matchesBundleByContainers.TryGetValue(containerKey, out WordMatchMeta[]? entityMatches))
                continue;

            foreach (WordMatchMeta wordMatchMeta in entityMatches)
                yield return wordMatchMeta;
        }
    }

    /// <summary>
    /// Бинарный поиск индекса нужного типа для бандла типов
    /// </summary>
    /// <param name="sortedKeys"></param>
    /// <param name="targetType"></param>
    /// <returns></returns>
    private static int BinarySearch(KeyValuePair<byte /*TypeId*/, Dictionary</*ByNodeKey*/ Key, WordMatchMeta[]>>[] sortedKeys, byte targetType)
    {
        int left = 0;
        int right = sortedKeys.Length - 1;

        while (left <= right)
        {
            int mid = left + (right - left) / 2;

            if (sortedKeys[mid].Key == targetType)
                return mid;

            if (sortedKeys[mid].Key < targetType)
                left = mid + 1;
            else
                right = mid - 1;
        }

        return -1;
    }
}
