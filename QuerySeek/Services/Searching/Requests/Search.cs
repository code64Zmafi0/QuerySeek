using QuerySeek.Models;
using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching.Requests;

/// <summary>
/// Выполняет поиск сущностей целевого типа
/// </summary>
/// <param name="targetType">Целевой тип сущности</param>
public class Search(byte targetType) : RequestBase(targetType)
{
    public override void ProcessRequest(SearchContextBase searchContext, CancellationToken ct)
    {
        QueryWordContainer[] queryWordsBundle = searchContext.SearchWordsBundle;
        KeyValuePair<byte, Dictionary<Key, WordMatchMeta[]>>[][] entitiesSearchMap = searchContext.Index.EntitiesSearchMap;

        for (byte queryWordPosition = 0; queryWordPosition < queryWordsBundle.Length; queryWordPosition++)
        {
            List<KeyValuePair<int, byte>> currentSimilarWordsBundle = queryWordsBundle[queryWordPosition].SimilarWords;

            WordsSearchManager wsm = searchContext.WordsSearchSettings.GetWordsSearchManager();

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
                    WordCompareResult wcr = new(
                        wordMatchMeta.NameWordPosition,
                        wordMatchMeta.NameType,
                        queryWordPosition,
                        indexWordInfo.Value);

                    searchContext.AddResult(entityKey, wcr);
                }
            }
        }
    }
}
