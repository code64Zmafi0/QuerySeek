using QuerySeek.Models;
using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching.Requests;

/// <summary>
/// Выполняет поиск сущностей целевого типа
/// </summary>
/// <param name="targetType">Целевой тип сущности</param>
/// <param name="filter">Фильтр добавления в словарь найденных</param>
public class Search(
    byte targetType,
    Func<Key, bool>? filter = null)
    : RequestBase(targetType)
{
    public override void ProcessRequest(SearchContextBase searchContext, CancellationToken ct)
    {
        List<KeyValuePair<int, byte>>[] wordsBundle = searchContext.SearchWordsBundle;
        KeyValuePair<byte, Dictionary<Key, WordMatchMeta[]>>[][] entitiesSearchMap = searchContext.Index.EntitiesSearchMap;

        for (byte queryWordPosition = 0; queryWordPosition < wordsBundle.Length; queryWordPosition++)
        {
            List<KeyValuePair<int, byte>> currentSimilarWordsBundle = wordsBundle[queryWordPosition];

            WordsSearchManager wsm = searchContext.WordsSearchSettings.GetWordsSearchManager();

            for (int wbIndex = 0; wbIndex < currentSimilarWordsBundle.Count; wbIndex++)
            {
                if (!wsm.NeedContinue)
                    break;

                KeyValuePair<int, byte> indexWordInfo = currentSimilarWordsBundle[wbIndex];

                int wordId = indexWordInfo.Key;

                WordMatchMeta[]? list = entitiesSearchMap.GetMatchesByWord(wordId, TargetType);

                if (list is null)
                    continue;

                wsm.IncrementMatch();

                foreach (WordMatchMeta wordMatchMeta in list)
                {
                    if (ct.IsCancellationRequested)
                        return;

                    Key entityKey = new(TargetType, wordMatchMeta.EntityId);

                    if (!((filter?.Invoke(entityKey)) ?? true))
                        continue;

                    WordCompareResult wcr = new(
                        wordMatchMeta.NameWordPosition,
                        wordMatchMeta.PhraseType,
                        queryWordPosition,
                        indexWordInfo.Value);

                    searchContext.AddResult(entityKey, wcr);
                }
            }
        }
    }
}
