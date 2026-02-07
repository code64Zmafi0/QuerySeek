using MessagePack;

namespace QuerySeek.Models;

[MessagePackObject]
public class EntitiesByWordsIndex()
{
    [Key(1)]
    public KeyValuePair<byte /*TypeId*/, Dictionary</*ByNodeKey*/ Key, WordMatchMeta[]>>[][/*WordId*/] EntitiesByWords { get; set; } = [];

    public WordMatchMeta[]? GetMatchesByWord(int wordId, byte entityType)
    {
        var wordMatches = EntitiesByWords[wordId];

        int index = BinarySearch(wordMatches, entityType);
        if (index == -1) return null;

        if (wordMatches[index].Value.TryGetValue(Key.Default, out var matches))
        {
            return matches;
        }

        return null;
    }

    public IEnumerable<WordMatchMeta> GetMatchesByWordAndParents(
        int wordId,
        byte entityType,
        Key[] parentKeys)
    {
        var wordMatches = EntitiesByWords[wordId];

        int index = BinarySearch(wordMatches, entityType);
        if (index == -1) yield break;

        var matchesBundle = wordMatches[index].Value;

        foreach (Key byKey in parentKeys)
        {
            if (!matchesBundle.TryGetValue(byKey, out var entityMatches))
                continue;

            foreach (WordMatchMeta wordMatchMeta in entityMatches)
                yield return wordMatchMeta;
        }
    }

    public void Trim()
    {
        foreach (var collection in EntitiesByWords)
        {
            foreach (var item in collection)
            {
                item.Value.TrimExcess();
            }
        }
    }

    public static int BinarySearch(KeyValuePair<byte /*TypeId*/, Dictionary</*ByNodeKey*/ Key, WordMatchMeta[]>>[] sortedKeys, byte targetType)
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
