using QuerySeek.Models;
using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching.Requests;

/// <summary>
/// Выполняет поиск сущностей целевого типа по контейнерам и текстовому запросу и добавляет в выборку
/// </summary>
/// <param name="targetType">Целевой тип сущности</param>
/// <param name="areasSelector">Выборка поисковых областей</param>
public class SearchByAreas(
    byte targetType,
    Func<IEnumerable<EntitySearchResult>> areasSelector) : EntitiesSelectionOperation(targetType)
{
    public override void Process(SearchContextBase searchContext, CancellationToken ct)
    {
        QueryWordContainer[] queryWordsBundle = searchContext.SearchWordsBundle;
        KeyValuePair<byte, IReadOnlyDictionary<Key, WordMatchMeta[]>>[][] entitiesSearchMap = searchContext.Index.EntitiesSearchMap;

        EntitySearchResult[] areas = [..areasSelector()];

        for (byte queryWordPosition = 0; queryWordPosition < queryWordsBundle.Length; queryWordPosition++)
        {
            QueryWordContainer wordFromQuery = queryWordsBundle[queryWordPosition];

            List<KeyValuePair<int, byte>> currentSimilarWordsBundle = wordFromQuery.SimilarWords;

            WordsSearchStopManager wsm = searchContext.WordsSearchSettings.Settings.GetWordsSearchStopManager(wordFromQuery.QueryWordAndAlternatives[0]);

            for (int i = 0; i < currentSimilarWordsBundle.Count; i++)
            {
                if (!wsm.NeedContinue)
                    break;

                KeyValuePair<int, byte> indexWordInfo = currentSimilarWordsBundle[i];

                int wordId = indexWordInfo.Key;

                IEnumerable<Key> areasKeys = areas
                    .Where(a => !a.WordsMatches
                        .Exists(wm => wm.WordsBundlePosition == queryWordPosition && wordFromQuery.PositionsInRequest.Length < 2))
                    .Select(a => a.Key);

                bool isMatchedWord = false;
                foreach (WordMatchMeta[] matches in entitiesSearchMap.GetMatchesByWordAndAreas(
                    wordId,
                    TargetType,
                    areasKeys))
                {
                    isMatchedWord = true;

                    foreach (WordMatchMeta wordMatchMeta in matches)
                    {
                        if (ct.IsCancellationRequested)
                            return;

                        Key entityKey = new(TargetType, wordMatchMeta.EntityId);
                        WordMatch wordMatch = new(
                            wordMatchMeta.NameWordPosition,
                            wordMatchMeta.NameType,
                            queryWordPosition,
                            indexWordInfo.Value);

                        searchContext.AddResult(entityKey, wordMatch);
                    }
                }

                if (isMatchedWord) wsm.IncrementMatch();
            }
        }
    }
}
