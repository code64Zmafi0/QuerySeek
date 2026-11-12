using QuerySeek.Models;
using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching.Selection;

/// <summary>
/// Выполняет поиск сущностей целевого типа по контейнерам и текстовому запросу и добавляет в выборку приоритизируя более редкие слова
/// </summary>
/// <param name="targetType">Целевой тип сущности</param>
/// <param name="areasSelector">Выборка поисковых областей</param>
/// <param name="similarsCountToStopProcess">Количество совпавших схожих слов для остановки поиска определнной сущности по текущему слову из запроса</param>
/// <param name="searchByMatchedTokens">Искать ли уже по совпавшим словам в родителе</param>
public class SearchByAreasWithTermFrequence(
    byte targetType,
    Func<IEnumerable<EntitySearchResult>> areasSelector,
    Func<QueryTokenContainer, int>? similarsCountToStopProcess = null,
    bool searchByMatchedTokens = true) : EntitiesSelectionOperation(targetType)
{
    public override void Process(SearchContextBase searchContext)
    {
        QueryTokenContainer[] queryWordsBundle = searchContext.SearchTokensBundle;
        KeyValuePair<byte, IReadOnlyDictionary<Key, WordMatchMeta[]>>[][] entitiesSearchMap = searchContext.Index.EntitiesSearchMap;

        EntitySearchResult[] areas = [.. areasSelector()];

        for (byte queryWordPosition = 0; queryWordPosition < queryWordsBundle.Length; queryWordPosition++)
        {
            QueryTokenContainer wordFromQuery = queryWordsBundle[queryWordPosition];

            List<KeyValuePair<int, byte>> currentSimilarWordsBundle = wordFromQuery.SimilarWords;

            ByWordSearchStopManager wsm = similarsCountToStopProcess is null
                ? new()
                : new(similarsCountToStopProcess(wordFromQuery));

            if (!wsm.NeedContinue) continue;

            IEnumerable<(int WordId, byte WordScore, WordMatchMeta[] WordMatches)> wordsMatches = GetMatchesByWordsScores(
                currentSimilarWordsBundle, entitiesSearchMap, areas, queryWordPosition, wordFromQuery)
                    .OrderByDescending(i => i.WordScore)
                    .ThenBy(i => i.WordMatches.Length);

            foreach ((int WordId, byte WordScore, WordMatchMeta[] WordMatches) in wordsMatches)
            {
                if (!wsm.NeedContinue) break;

                foreach (WordMatchMeta wordMatchMeta in WordMatches)
                {
                    Key entityKey = new(TargetType, wordMatchMeta.EntityId);
                    TokenMatch wordMatch = new(
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
        QueryTokenContainer wordFromQuery)
    {
        for (int i = 0; i < similarWords.Count; i++)
        {
            KeyValuePair<int, byte> wordInfo = similarWords[i];

            IReadOnlyDictionary<Key, WordMatchMeta[]>? matchesByContainers = entitiesSearchMap.GetMatchesByAresDict(
                wordInfo.Key,
                TargetType);

            if (matchesByContainers == null) continue;

            if (searchByMatchedTokens)
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
                    if (entitySearchResult.Matches.Exists(wm => wm.SearchTokensBundlePosition == queryWordPosition
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
