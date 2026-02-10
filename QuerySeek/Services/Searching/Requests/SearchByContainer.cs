using System.Runtime.CompilerServices;
using QuerySeek.Models;

namespace QuerySeek.Services.Searching.Requests;

/// <summary>
/// Выполняет поиск сущностей целевого типа по найденным контейнерам
/// </summary>
/// <param name="targetType">Целевой тип сущности</param>
/// <param name="containerType">Тип сущности родителя (Parent)</param>
/// <param name="filter">Фильтр добавления в словарь найденных</param>
/// <param name="containersFilter">Фильтр родителей по которым осуществляем поиск</param>
public class SearchByContainer(
    byte targetType,
    byte containerType,
    Func<Key, bool>? filter = null,
    Func<IEnumerable<EntityMatchesBundle>, IEnumerable<EntityMatchesBundle>>? containersFilter = null) : RequestBase(targetType)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual Key[] SelectParents(Dictionary<Key, EntityMatchesBundle> byStrat)
        => [.. containersFilter is null
            ? byStrat.Keys
            : containersFilter.Invoke(byStrat.Values).Select(i => i.Key)];

    public override void ProcessRequest(
        SearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        PerfomanceSettings perfomance,
        CancellationToken ct)
    {
        Dictionary<Key, EntityMeta> entites = searchContext.Index.Entities;
        EntitiesByWordsIndex entitiesByWordsIndex = searchContext.Index.EntitiesByWordsIndex;

        if (!(searchContext.GetResultsByType(containerType) is { } byStrat))
            return;

        Key[] parents = SelectParents(byStrat);

        for (byte queryWordPosition = 0; queryWordPosition < wordsBundle.Length; queryWordPosition++)
        {
            List<KeyValuePair<int, byte>> currentBundle = wordsBundle[queryWordPosition];

            Perfomancer perfomancer = perfomance.GetPerfomancer();

            for (int i = 0; i < currentBundle.Count; i++)
            {
                if (!perfomancer.NeedContinue)
                    break;

                KeyValuePair<int, byte> indexWordInfo = currentBundle[i];

                int wordId = indexWordInfo.Key;

                bool isMatchedWord = false;
                foreach (var wordMatchMeta in entitiesByWordsIndex.GetMatchesByWordAndParents(
                    wordId,
                    TargetType,
                    parents))
                {
                    if (ct.IsCancellationRequested)
                        return;

                    isMatchedWord = true;

                    Key entityKey = new(TargetType, wordMatchMeta.EntityId);
                    EntityMeta entityMeta = entites[entityKey];

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

                if (isMatchedWord) perfomancer.IncrementMatch();
            }
        }
    }
}
