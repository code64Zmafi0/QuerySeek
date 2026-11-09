using QuerySeek.Models;
using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching.Selection;

/// <summary>
/// Выполняет поиск сущностей целевого типа по текстовому запросу и добавляет в выборку приоритизируя более редкие слова
/// </summary>
/// <param name="targetType">Целевой тип сущности</param>
/// <param name="validateWord">Функция для валидации слов из запроса</param>
/// <param name="similarsCountToStopProcess">Количество совпавших схожих слов для остановки поиска определнной сущности по текущему слову из запроса</param>
public class SearchWithTermFrequence(
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

            IEnumerable<(int WordId, byte WordScore, WordMatchMeta[] WordMatches)> wordsMatches = GetMatchesByWordsScores(currentSimilarWordsBundle, entitiesSearchMap)
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
        KeyValuePair<byte, IReadOnlyDictionary<Key, WordMatchMeta[]>>[][] entitiesSearchMap)
    {
        for (int i = 0; i < similarWords.Count; i++)
        {
            KeyValuePair<int, byte> wordInfo = similarWords[i];

            WordMatchMeta[]? matches = entitiesSearchMap.GetMatchesByWord(wordInfo.Key, TargetType);

            if (matches != null) yield return (wordInfo.Key, wordInfo.Value, matches);
        }
    }
}
