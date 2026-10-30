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
public class SearchByAreas(
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

            for (int i = 0; i < currentSimilarWordsBundle.Count; i++)
            {
                if (!wsm.NeedContinue)
                    break;

                KeyValuePair<int, byte> indexWordInfo = currentSimilarWordsBundle[i];

                int wordId = indexWordInfo.Key;

                IEnumerable<Key> areasKeys = needSearchByMatchedTokens
                    ? areas.Select(a => a.Key)
                    : areas
                        .Where(a => !a.WordsMatches.Exists(wm => wm.WordsBundlePosition == queryWordPosition && wordFromQuery.PositionsInRequest.Length < 2))
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

                        AddResult(searchContext, entityKey, wordMatch);
                    }
                }

                if (isMatchedWord) wsm.IncrementMatch();
            }
        }
    }
}
