using QuerySeek.Models;

namespace QuerySeek.Services.Searching.Requests;

/// <summary>
/// Выполняет принудительное добавление дочерних элементов по родителю в котейнерах в выдачу
/// </summary>
/// <param name="targetType">Целевой тип</param>
/// <param name="parentType">Тип родителя</param>
/// <param name="containerType">Тип контейнера</param>
/// <param name="appendFilter">Фильтр дочерних сущностей КАЖДОГО родителя</param>
public class AppendChildsByContainers(
    byte targetType,
    byte parentType,
    byte containerType,
    Func<IEnumerable<Key>, IEnumerable<Key>> appendFilter) : RequestBase(targetType)
{
    public override void ProcessRequest(
        SearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        WordsSearchSettings wordsSearchSettings,
        CancellationToken ct)
    {
        Dictionary<Key, EntityMeta> entities = searchContext.Index.Entities;

        if (!(searchContext.GetResultsByType(parentType) is { } from)
            || !(searchContext.GetResultsByType(containerType) is { } containers))
            return;

        IEnumerable<Key> GetChilds(IEnumerable<Key> parents)
        {
            foreach(Key containerKey in parents)
            {
                if (!entities.TryGetValue(containerKey, out EntityMeta? meta))
                    continue;

                foreach(Key child in meta.Childs.Where(i => i.Type == TargetType))
                    yield return child;
            }
        }

        //Количество данных в контейнере меньше чем суммарно связей - реализация интерсект скрывает создание хешсет из второго аргумента
        IEnumerable<Key> childs = GetChilds(from.Keys).Intersect(GetChilds(containers.Keys));

        foreach (Key child in appendFilter(childs))
            searchContext.AddResult(child);
    }
}