using QuerySeek.Models;

namespace QuerySeek.Searching.Selection;

/// <summary>
/// Выполняет принудительное добавление дочерних элементов по родителю в <see cref="SearchContextBase.SearchResult"/>
/// </summary>
/// <param name="targetType">Целевой тип</param>
/// <param name="appendFilter">Фильтр добавляемых сущностей для каждого родителя</param>
/// <param name="parentsSelector">Выборка родителей по которым отбираем дочерние сущности</param>
public class AppendChilds(
    byte targetType,
    Func<IEnumerable<Key>, IEnumerable<Key>> appendFilter,
    Func<IEnumerable<EntitySearchResult>> parentsSelector) : EntitiesSelectionOperation(targetType)
{
    public override void Process(SearchContextBase searchContext)
    {
        foreach (EntitySearchResult parent in parentsSelector())
        {
            foreach (Key child in appendFilter(parent.GetChilds(TargetType)))
                AddResult(searchContext, child);
        }
    }
}
