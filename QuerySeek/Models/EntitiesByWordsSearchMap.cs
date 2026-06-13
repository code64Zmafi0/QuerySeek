using MessagePack;

namespace QuerySeek.Models;

/// <summary>
/// Индекс сущностей по словам. id слова - индекс массива, внутри разложены матчи отдельно для каждого типа, внутри словари по контейнерам
/// </summary>
[MessagePackObject]
public class EntitiesByWordsSearchMap()
{
    /// <summary>
    /// Словарь WordId -> Types -> Containers -> MatchesToEntites
    /// Так как ид слов последовательны использован массив вместо словоря - так как словарь большого размера в разы медленней на обращени
    /// </summary>
    [Key(1)]
    public KeyValuePair<byte /*TypeId*/, Dictionary</*ByNodeKey*/ Key, WordMatchMeta[]>>[][/*WordId*/] EntitiesByWords { get; set; } = [];

    public WordMatchMeta[]? GetMatchesByWord(int wordId, byte entityType)
    {
        KeyValuePair<byte, Dictionary<Key, WordMatchMeta[]>>[] wordMatches = EntitiesByWords[wordId];

        //Типы в бандле упорядочены. Используем бинарный поиск.
        int index = BinarySearch(wordMatches, entityType);
        if (index == -1) return null;

        if (wordMatches[index].Value.TryGetValue(Key.Default, out WordMatchMeta[]? matches))
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
        KeyValuePair<byte, Dictionary<Key, WordMatchMeta[]>>[] wordMatches = EntitiesByWords[wordId];

        //Типы в бандле упорядочены. Используем бинарный поиск.
        int index = BinarySearch(wordMatches, entityType);
        if (index == -1) yield break;

        Dictionary<Key, WordMatchMeta[]> matchesBundle = wordMatches[index].Value;

        foreach (Key byKey in parentKeys)
        {
            if (!matchesBundle.TryGetValue(byKey, out WordMatchMeta[]? entityMatches))
                continue;

            foreach (WordMatchMeta wordMatchMeta in entityMatches)
                yield return wordMatchMeta;
        }
    }

    public void Trim()
    {
        foreach (KeyValuePair<byte, Dictionary<Key, WordMatchMeta[]>>[] collection in EntitiesByWords)
        {
            foreach (KeyValuePair<byte, Dictionary<Key, WordMatchMeta[]>> item in collection)
            {
                item.Value.TrimExcess();
            }
        }
    }

    /// <summary>
    /// Бинарный поиск для бандла типов
    /// </summary>
    /// <param name="sortedKeys"></param>
    /// <param name="targetType"></param>
    /// <returns></returns>
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
