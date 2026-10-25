using QuerySeek.Models;
using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching.Selection;

/// <summary>
/// Выполняет поиск сущностей целевого типа по текстовому запросу и добавляет в выборку
/// </summary>
/// <param name="targetType">Целевой тип сущности</param>
/// <param name="validWordsFilter">Функция для валидации слов из запроса</param>
public class Search(
    byte targetType,
    Func<QueryWordContainer, bool>? validWordsFilter = null)
    : EntitiesSelectionOperation(targetType)
{
    public override void Process(SearchContextBase searchContext, CancellationToken ct)
    {
        QueryWordContainer[] queryWordsBundle = searchContext.SearchWordsBundle;
        KeyValuePair<byte, IReadOnlyDictionary<Key, WordMatchMeta[]>>[][] entitiesSearchMap = searchContext.Index.EntitiesSearchMap;

        for (byte queryWordPosition = 0; queryWordPosition < queryWordsBundle.Length; queryWordPosition++)
        {
            QueryWordContainer wordFromQuery = queryWordsBundle[queryWordPosition];

            if (validWordsFilter != null && !validWordsFilter(wordFromQuery)) continue;

            List<KeyValuePair<int, byte>> currentSimilarWordsBundle = wordFromQuery.SimilarWords;

            WordsSearchStopManager wsm = searchContext.WordsSearchSettings.Settings.GetWordsSearchStopManager(wordFromQuery.Word);

            for (int wbIndex = 0; wbIndex < currentSimilarWordsBundle.Count; wbIndex++)
            {
                if (!wsm.NeedContinue)
                    break;

                KeyValuePair<int, byte> indexWordInfo = currentSimilarWordsBundle[wbIndex];

                int wordId = indexWordInfo.Key;

                WordMatchMeta[]? matches = entitiesSearchMap.GetMatchesByWord(wordId, TargetType);

                if (matches is null)
                    continue;

                wsm.IncrementMatch();

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
        }
    }
}
