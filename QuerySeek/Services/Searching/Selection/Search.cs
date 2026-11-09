using QuerySeek.Models;
using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching.Selection;

/// <summary>
/// Выполняет поиск сущностей целевого типа по текстовому запросу и добавляет в выборку
/// </summary>
/// <param name="targetType">Целевой тип сущности</param>
/// <param name="validateWord">Функция для валидации слов из запроса</param>
/// <param name="similarsCountToStopProcess">Количество совпавших схожих слов для остановки поиска определнной сущности по текущему слову из запроса</param>
public class Search(
    byte targetType,
    Func<QueryWordContainer, bool>? validateWord = null,
    Func<QueryWordContainer, int>? similarsCountToStopProcess = null)
    : EntitiesSelectionOperation(targetType)
{
    public override void Process(SearchContextBase searchContext, CancellationToken ct)
    {
        QueryWordContainer[] queryWordsBundle = searchContext.SearchWordsBundle;
        KeyValuePair<byte, IReadOnlyDictionary<Key, WordMatchMeta[]>>[][] entitiesSearchMap = searchContext.Index.EntitiesSearchMap;

        for (byte queryWordPosition = 0; queryWordPosition < queryWordsBundle.Length; queryWordPosition++)
        {
            QueryWordContainer wordFromQuery = queryWordsBundle[queryWordPosition];

            if (validateWord != null && !validateWord(wordFromQuery)) continue;

            List<KeyValuePair<int, byte>> currentSimilarWordsBundle = wordFromQuery.SimilarWords;

            ByWordSearchStopManager wsm = similarsCountToStopProcess is null
                ? new()
                : new(similarsCountToStopProcess(wordFromQuery));

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
