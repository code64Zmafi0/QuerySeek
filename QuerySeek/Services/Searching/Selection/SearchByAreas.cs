using QuerySeek.Models;
using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching.Selection;

/// <summary>
/// Выполняет поиск сущностей целевого типа по контейнерам и текстовому запросу и добавляет в выборку
/// </summary>
/// <param name="targetType">Целевой тип сущности</param>
/// <param name="areasSelector">Выборка поисковых областей</param>
/// <param name="similarsCountToStopProcess">Количество совпавших схожих слов для остановки поиска определнной сущности по текущему слову из запроса</param>
/// <param name="searchByMatchedTokens">Искать ли по уже совпавшим словам в родителе</param>
public class SearchByAreas(
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

            for (int i = 0; i < currentSimilarWordsBundle.Count; i++)
            {
                if (!wsm.NeedContinue)
                    break;

                KeyValuePair<int, byte> indexWordInfo = currentSimilarWordsBundle[i];

                int wordId = indexWordInfo.Key;

                IEnumerable<Key> areasKeys = searchByMatchedTokens
                    ? areas.Select(a => a.Key)
                    : areas
                        .Where(a => !a.Matches.Exists(wm => wm.SearchTokensBundlePosition == queryWordPosition && wordFromQuery.PositionsInRequest.Length < 2))
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
                        Key entityKey = new(TargetType, wordMatchMeta.EntityId);
                        TokenMatch wordMatch = new(
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
