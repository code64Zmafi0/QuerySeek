using QuerySeek.Models;

namespace QuerySeek.Services.Searching.Requests;

/// <summary>
/// Выполняет принудительное добавление дочерних элементов по родителю в котейнерах в выдачу
/// </summary>
/// <param name="targetType">Целевой тип</param>
/// <param name="parentType">Тип родителя</param>
/// <param name="containerType">Тип контейнера</param>
/// <param name="appendFilter">Фильтр добавляемых сущностей</param>
/// <param name="parentsFilter">Фильтр родителей</param>
/// <param name="containersFilter">Фильтр контейнеров/param>
public class AppendChildsByContainers(
    byte targetType,
    byte parentType,
    byte containerType,
    Func<IEnumerable<Key>, IEnumerable<Key>> appendFilter,
    Func<ICollection<EntitySearchResult>, IEnumerable<EntitySearchResult>>? parentsFilter = null,
    Func<ICollection<EntitySearchResult>, IEnumerable<EntitySearchResult>>? containersFilter = null) : RequestBase(targetType)
{
    public override void ProcessRequest(SearchContextBase searchContext, CancellationToken ct)
    {
        if (!(searchContext.GetResultsByType(parentType) is { } parents)
            || !(searchContext.GetResultsByType(containerType) is { } containers))
            return;

        IEnumerable<Key> GetChilds(
            ICollection<EntitySearchResult> from,
            Func<ICollection<EntitySearchResult>, IEnumerable<EntitySearchResult>>? selector)
        {
            IEnumerable<EntitySearchResult> fromData = selector is null
                ? from
                : selector(from);

            foreach (EntitySearchResult containerInfo in fromData)
            {
                foreach(Key child in containerInfo.Meta.Childs.Where(i => i.Type == TargetType))
                    yield return child;
            }
        }

        //Количество данных в контейнере меньше чем суммарно связей - реализация интерсект скрывает создание хешсет из второго аргумента
        IEnumerable<Key> childs = GetChilds(parents.Values, parentsFilter).Intersect(GetChilds(containers.Values, containersFilter));

        foreach (Key child in appendFilter(childs))
            searchContext.AddResult(child);
    }
}