using QuerySeek.Models;
using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching.Requests;

/// <summary>
/// Выполняет поиск сущностей целевого типа по найденным контейнерам
/// </summary>
/// <param name="targetType">Целевой тип сущности</param>
/// <param name="containerType">Тип сущности контейнера</param>
/// <param name="containersFilter">Фильтр контейнеров по которым осуществляем поиск</param>
public class SearchByContainer(
    byte targetType,
    byte containerType,
    Func<ICollection<EntitySearchResult>, IEnumerable<EntitySearchResult>>? containersFilter = null) : RequestBase(targetType)
{
    public override void ProcessRequest(SearchContextBase searchContext, CancellationToken ct)
    {
        QueryWordContainer[] queryWordsBundle = searchContext.SearchWordsBundle;
        KeyValuePair<byte, Dictionary<Key, WordMatchMeta[]>>[][] entitiesSearchMap = searchContext.Index.EntitiesSearchMap;

        if (!searchContext.TryGetResultsByType(containerType, out var containersResult))
            return;

        Key[] containers = SelectContainers(containersResult);

        for (byte queryWordPosition = 0; queryWordPosition < queryWordsBundle.Length; queryWordPosition++)
        {
            QueryWordContainer wordFromQuery = queryWordsBundle[queryWordPosition];
            List<KeyValuePair<int, byte>> currentSimilarWordsBundle = wordFromQuery.SimilarWords;

            WordsSearchStopManager wsm = searchContext.WordsSearchSettings.GetWordsSearchStopManager(wordFromQuery.QueryWord);

            for (int i = 0; i < currentSimilarWordsBundle.Count; i++)
            {
                if (!wsm.NeedContinue)
                    break;

                KeyValuePair<int, byte> indexWordInfo = currentSimilarWordsBundle[i];

                int wordId = indexWordInfo.Key;

                bool isMatchedWord = false;
                foreach (WordMatchMeta[] matches in entitiesSearchMap.GetMatchesByWordAndContainers(
                    wordId,
                    TargetType,
                    containers))
                {
                    isMatchedWord = true;

                    foreach (WordMatchMeta wordMatchMeta in matches)
                    {
                        if (ct.IsCancellationRequested)
                            return;

                        Key entityKey = new(TargetType, wordMatchMeta.EntityId);
                        WordCompareResult wcr = new(
                            wordMatchMeta.NameWordPosition,
                            wordMatchMeta.NameType,
                            queryWordPosition,
                            indexWordInfo.Value);

                        searchContext.AddResult(entityKey, wcr);
                    }
                }

                if (isMatchedWord) wsm.IncrementMatch();
            }
        }
    }

    private Key[] SelectContainers(Dictionary<Key, EntitySearchResult> containers)
    {
        Key[] result;

        if (containersFilter is null)
        {
            result = new Key[containers.Count];
            containers.Keys.CopyTo(result, 0);
        }
        else
        {
            result = [.. containersFilter.Invoke(containers.Values).Select(i => i.Key)];
        }

        return result;
    }
}
