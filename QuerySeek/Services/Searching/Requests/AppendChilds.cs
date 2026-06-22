using QuerySeek.Models;

namespace QuerySeek.Services.Searching.Requests;

/// <summary>
/// Выполняет принудительное добавление дочерних элементов по родителю в выдачу
/// </summary>
/// <param name="targetType">Целевой тип</param>
/// <param name="parentType">Тип родителя</param>
/// <param name="appendFilter">Фильтр добавляемых сущностей для каждого родителя</param>
/// <param name="parentsFilter">Фильтр родителей по которым отбираем дочерние сущности</param>
public class AppendChilds(
    byte targetType,
    byte parentType,
    Func<IEnumerable<Key>, IEnumerable<Key>> appendFilter,
    Func<IEnumerable<EntitySearchResult>, IEnumerable<EntitySearchResult>>? parentsFilter = null) : RequestBase(targetType)
{
    public override void ProcessRequest(SearchContextBase searchContext, CancellationToken ct)
    {
        Dictionary<Key, EntityMeta> entities = searchContext.Index.Entities;

        if (!(searchContext.GetResultsByType(parentType) is { } parents))
            return;

        IEnumerable<EntitySearchResult> GetParents()
            => parentsFilter is null
                ? parents.Values
                : parentsFilter(parents.Values);

        foreach (EntitySearchResult parent in GetParents())
        {
            if (ct.IsCancellationRequested)
                break;

            Key[] parentEntityChilds = parent.Meta.Childs;

            foreach (Key child in appendFilter(parentEntityChilds.Where(i => i.Type == TargetType)))
            {
                searchContext.AddResult(child);
            }
        }
    }
}