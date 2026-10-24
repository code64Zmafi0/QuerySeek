using QuerySeek.Models;

namespace QuerySeek.Services.Searching.Requests;

/// <summary>
/// Выполняет принудительное добавление дочерних элементов по родителю в выборку
/// </summary>
/// <param name="targetType">Целевой тип</param>
/// <param name="appendFilter">Фильтр добавляемых сущностей для каждого родителя</param>
/// <param name="parentsSelector">Выборка родителей по которым отбираем дочерние сущности</param>
public class AppendChilds(
    byte targetType,
    Func<IEnumerable<Key>, IEnumerable<Key>> appendFilter,
    Func<IEnumerable<EntitySearchResult>> parentsSelector) : EntitiesSelectionOperation(targetType)
{
    public override void Process(SearchContextBase searchContext, CancellationToken ct)
    {
        foreach (EntitySearchResult parent in parentsSelector())
        {
            if (ct.IsCancellationRequested)
                break;

            Key[] parentEntityChilds = parent.Meta.Childs;

            foreach (Key child in appendFilter(parentEntityChilds.Where(i => i.Type == TargetType)))
                AddResult(searchContext, child);
        }
    }
}
