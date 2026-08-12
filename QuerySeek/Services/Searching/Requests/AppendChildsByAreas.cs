using QuerySeek.Models;

namespace QuerySeek.Services.Searching.Requests;

/// <summary>
/// Выполняет принудительное добавление дочерних элементов по родителю в котейнерах в выдачу
/// </summary>
/// <param name="targetType">Целевой тип</param>
/// <param name="appendFilter">Фильтр добавляемых сущностей</param>
/// <param name="parentsSelector">Выборка родителей</param>
/// <param name="areasSelector">Выборка поисковых областей/param>
public class AppendChildsByAreas(
    byte targetType,
    Func<IEnumerable<Key>, IEnumerable<Key>> appendFilter,
    IEnumerable<EntitySearchResult> parentsSelector,
    IEnumerable<EntitySearchResult> areasSelector) : RequestBase(targetType)
{
    public override void ProcessRequest(SearchContextBase searchContext, CancellationToken ct)
    {
        IEnumerable<Key> GetChilds(IEnumerable<EntitySearchResult> selector)
        {
            foreach (EntitySearchResult containerInfo in selector)
            {
                foreach(Key child in containerInfo.Meta.Childs.Where(i => i.Type == TargetType))
                    yield return child;
            }
        }

        //Количество данных в контейнере меньше чем суммарно связей - реализация интерсект скрывает создание хешсет из второго аргумента
        IEnumerable<Key> childs = GetChilds(parentsSelector).Intersect(appendFilter(GetChilds(areasSelector)));

        foreach (Key child in childs)
            searchContext.AddResult(child);
    }
}