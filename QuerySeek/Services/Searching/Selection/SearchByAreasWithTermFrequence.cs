using QuerySeek.Models;
using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching.Selection;

/// <summary>
/// Выполняет поиск сущностей целевого типа по контейнерам и текстовому запросу и добавляет в выборку
/// </summary>
/// <param name="targetType">Целевой тип сущности</param>
/// <param name="areasSelector">Выборка поисковых областей</param>
/// <param name="needSearchByMatchedTokens">Искать ли уже по совпавшим словам в родителе</param>
/// <param name="validWordsFilter">Функция для валидации слов из запроса</param>
public class SearchByAreasWithTermFrequence(
    byte targetType,
    Func<IEnumerable<EntitySearchResult>> areasSelector,
    bool needSearchByMatchedTokens = true,
    Func<QueryWordContainer, bool>? validWordsFilter = null) : EntitiesSelectionOperation(targetType)
{
    public override void Process(SearchContextBase searchContext, CancellationToken ct)
    {
        QueryWordContainer[] queryWordsBundle = searchContext.SearchWordsBundle;
        KeyValuePair<byte, IReadOnlyDictionary<Key, WordMatchMeta[]>>[][] entitiesSearchMap = searchContext.Index.EntitiesSearchMap;

        EntitySearchResult[] areas = [.. areasSelector()];

        for (byte queryWordPosition = 0; queryWordPosition < queryWordsBundle.Length; queryWordPosition++)
        {
            QueryWordContainer wordFromQuery = queryWordsBundle[queryWordPosition];

            if (validWordsFilter != null && !validWordsFilter(wordFromQuery)) continue;

            List<KeyValuePair<int, byte>> currentSimilarWordsBundle = wordFromQuery.SimilarWords;

            WordsSearchStopManager wsm = searchContext.WordsSearchSettings.Settings.GetWordsSearchStopManager(wordFromQuery.Word);

            IEnumerable<(int WordId, byte WordScore, WordMatchMeta[] WordMatches)> wordsMatches = GetMatchesByWordsScores(
                currentSimilarWordsBundle, entitiesSearchMap, areas, queryWordPosition, wordFromQuery)
                    .OrderByDescending(i => i.WordScore)
                    .ThenBy(i => i.WordMatches.Length);

            foreach ((int WordId, byte WordScore, WordMatchMeta[] WordMatches) in wordsMatches)
            {
                if (!wsm.NeedContinue) break;

                foreach (WordMatchMeta wordMatchMeta in WordMatches)
                {
                    if (ct.IsCancellationRequested)
                        return;

                    Key entityKey = new(TargetType, wordMatchMeta.EntityId);
                    WordMatch wordMatch = new(
                        wordMatchMeta.NameWordPosition,
                        wordMatchMeta.NameType,
                        queryWordPosition,
                        WordScore);

                    AddResult(searchContext, entityKey, wordMatch);
                }

                wsm.IncrementMatch();
            }
        }
    }

    private IEnumerable<(int WordId, byte WordScore, WordMatchMeta[] WordMatches)> GetMatchesByWordsScores(
        List<KeyValuePair<int, byte>> similarWords,
        KeyValuePair<byte, IReadOnlyDictionary<Key, WordMatchMeta[]>>[][] entitiesSearchMap,
        EntitySearchResult[] areas,
        byte queryWordPosition,
        QueryWordContainer wordFromQuery)
    {
        for (int i = 0; i < similarWords.Count; i++)
        {
            KeyValuePair<int, byte> wordInfo = similarWords[i];

            IReadOnlyDictionary<Key, WordMatchMeta[]>? matchesByContainers = entitiesSearchMap.GetMatchesByAresDict(
                wordInfo.Key,
                TargetType);

            if (matchesByContainers == null) continue;

            if (needSearchByMatchedTokens)
            {
                foreach (Key key in areas.Select(i => i.Key))
                {
                    if (!matchesByContainers.TryGetValue(key, out WordMatchMeta[]? matches))
                        continue;

                    yield return (wordInfo.Key, wordInfo.Value, matches);
                }
            }
            else
            {
                foreach (EntitySearchResult entitySearchResult in areas)
                {
                    if (entitySearchResult.WordsMatches.Exists(wm => wm.WordsBundlePosition == queryWordPosition
                                                                  && wordFromQuery.PositionsInRequest.Length < 2))
                        continue;

                    if (!matchesByContainers.TryGetValue(entitySearchResult.Key, out WordMatchMeta[]? matches))
                        continue;

                    yield return (wordInfo.Key, wordInfo.Value, matches);
                }
            }
        }
    }
}
