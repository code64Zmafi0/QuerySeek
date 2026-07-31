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
    Func<IEnumerable<EntitySearchResult>, IEnumerable<EntitySearchResult>>? containersFilter = null) : RequestBase(targetType)
{
    public EntitySearchResult[] SelectContainers(Dictionary<Key, EntitySearchResult> containers)
    {
        EntitySearchResult[] result;

        if (containersFilter is null)
        {
            result = new EntitySearchResult[containers.Count];
            containers.Values.CopyTo(result, 0);
        }
        else
        {
            result = [.. containersFilter.Invoke(containers.Values)];
        }

        return result;
    }

    public override void ProcessRequest(SearchContextBase searchContext, CancellationToken ct)
    {
        List<KeyValuePair<int, byte>>[] wordsBundle = searchContext.SearchWordsBundle;
        KeyValuePair<byte, Dictionary<Key, WordMatchMeta[]>>[][] entitiesSearchMap = searchContext.Index.EntitiesSearchMap;

        if (!(searchContext.GetResultsByType(containerType) is { } containersResult))
            return;

        EntitySearchResult[] containers = SelectContainers(containersResult);

        for (byte queryWordPosition = 0; queryWordPosition < wordsBundle.Length; queryWordPosition++)
        {
            List<KeyValuePair<int, byte>> currentBundle = wordsBundle[queryWordPosition];

            WordsSearchManager wsm = searchContext.WordsSearchSettings.GetWordsSearchManager();

            for (int i = 0; i < currentBundle.Count; i++)
            {
                if (!wsm.NeedContinue)
                    break;

                KeyValuePair<int, byte> indexWordInfo = currentBundle[i];

                int wordId = indexWordInfo.Key;

                bool isMatchedWord = false;
                foreach ((EntitySearchResult container, WordMatchMeta[] matches) in entitiesSearchMap.GetMatchesByWordAndContainers(
                    wordId,
                    TargetType,
                    containers))
                {
                    if (ct.IsCancellationRequested)
                        return;

                    if (container.ContainsQueryWord(queryWordPosition))
                        continue;

                    isMatchedWord = true;

                    foreach (WordMatchMeta wordMatchMeta in matches)
                    {
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
}
