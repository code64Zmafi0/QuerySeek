using QuerySeek.Models;

namespace QuerySeek.Services.Searching.Requests;

/// <summary>
/// Выполняет принудительное добавление сущностей целевого типа в выборку
/// </summary>
/// <param name="targetType"></param>
/// <param name="ids"></param>
public class Select(byte targetType, IEnumerable<int> ids) : EntitiesSelectionOperation(targetType)
{
    public override void Process(SearchContextBase searchContext, CancellationToken ct)
    {
        IReadOnlyDictionary<Key, EntityMeta> entities = searchContext.Index.Entities;

        foreach (int id in ids)
        {
            Key key = new(TargetType, id);

            if (entities.ContainsKey(key))
            {
                AddResult(searchContext, key);
            }
        }
    }
}
