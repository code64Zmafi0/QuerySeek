using QuerySeek.Models;

namespace QuerySeek.Services.Searching.Requests;

/// <summary>
/// Выполняет принудительное добавление дочерних элементов по родителю в выдачу
/// </summary>
/// <param name="targetType">Целевой тип</param>
/// <param name="parentType">Тип родителя</param>
/// <param name="appendFilter">Фильтр дочерних сущностей КАЖДОГО родителя</param>
/// <param name="parentTop">Топ родителей по prescore для добавления дочерних</param>
public class AppendChilds(
    byte targetType,
    byte parentType,
    Func<IEnumerable<Key>, IEnumerable<Key>> appendFilter,
    int parentTop = 0) : RequestBase(targetType)
{
    public override void ProcessRequest(SearchContextBase searchContext, CancellationToken ct)
    {
        Dictionary<Key, EntityMeta> entities = searchContext.Index.Entities;

        if (!(searchContext.GetResultsByType(parentType) is { } parents))
            return;

        IEnumerable<Key> GetKeys()
        {
            if (parentTop < 1)
                return parents.Keys;
            else
                return parents.Values
                    .OrderByDescending(i => i.Prescore)
                    .Take(parentTop)
                    .Select(i => i.Key);
        }

        foreach (Key parentKey in GetKeys())
        {
            if (ct.IsCancellationRequested)
                break;

            if (!(entities.TryGetValue(parentKey, out var parentMeta)))
                continue;

            Key[] parentEntityChilds = parentMeta.Childs;

            foreach (Key child in appendFilter(parentEntityChilds.Where(i => i.Type == TargetType)))
            {
                searchContext.AddResult(child);
            }
        }
    }
}