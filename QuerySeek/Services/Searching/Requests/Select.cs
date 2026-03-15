using QuerySeek.Models;

namespace QuerySeek.Services.Searching.Requests;

/// <summary>
/// Выполняет принудительное добавление сущностей целевого типа
/// </summary>
/// <param name="targetType"></param>
/// <param name="ids"></param>
public class Select(byte targetType, IEnumerable<int> ids) : RequestBase(targetType)
{
    public override void ProcessRequest(
        SearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        WordsSearchSettings wordsSearchSettings,
        CancellationToken ct)
    {
        Dictionary<Key, EntityMeta> entities = searchContext.Index.Entities;

        foreach (int id in ids)
        {
            Key key = new(TargetType, id);

            if (entities.ContainsKey(key))
            {
                searchContext.AddResult(key);
            }
        }
    }
}
