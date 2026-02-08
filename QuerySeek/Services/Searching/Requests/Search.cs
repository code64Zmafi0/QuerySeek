using QuerySeek.Models;

namespace QuerySeek.Services.Searching.Requests;

/// <summary>
/// Выполняет поиск сущностей целевого типа
/// </summary>
/// <param name="entityType">Целевой тип сущности</param>
/// <param name="filter">Фильтр добавления в словарь найденных</param>
public class Search(
    byte entityType,
    Func<Key, bool>? filter = null)
    : RequestBase(entityType)
{
    public override void ProcessRequest(
        SearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        PerfomanceSettings perfomance,
        CancellationToken ct)
    {
        EntitiesByWordsIndex entitiesByWordsIndex = searchContext.Index.EntitiesByWordsIndex;
        Dictionary<Key, EntityMeta> entities = searchContext.Index.Entities;

        for (byte queryWordPosition = 0; queryWordPosition < wordsBundle.Length; queryWordPosition++)
        {
            List<KeyValuePair<int, byte>> currentSimilarWordsBundle = wordsBundle[queryWordPosition];

            Perfomancer perfomancer = perfomance.GetPerfomancer();

            for (int wbIndex = 0; wbIndex < currentSimilarWordsBundle.Count; wbIndex++)
            {
                if (!perfomancer.NeedContinue)
                    break;

                KeyValuePair<int, byte> indexWordInfo = currentSimilarWordsBundle[wbIndex];

                int wordId = indexWordInfo.Key;

                WordMatchMeta[]? list = entitiesByWordsIndex.GetMatchesByWord(wordId, TargetType);

                if (list is null)
                    continue;

                perfomancer.IncrementMatch();

                foreach (WordMatchMeta wordMatchMeta in list)
                {
                    if (ct.IsCancellationRequested)
                        return;

                    Key entityKey = new(TargetType, wordMatchMeta.EntityId);
                    EntityMeta entityMeta = entities[entityKey];

                    if (!((filter?.Invoke(entityKey)) ?? true))
                        continue;

                    searchContext.AddResult(
                        entityKey,
                        entityMeta,
                        wordMatchMeta.NameWordPosition,
                        wordMatchMeta.PhraseType,
                        queryWordPosition,
                        indexWordInfo.Value);
                }
            }
        }
    }
}
