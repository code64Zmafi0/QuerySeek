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
        PerfomanceSettings perfomanceSettings,
        CancellationToken ct)
    {
        foreach (int id in ids)
        {
            searchContext.AddResult(new(TargetType, id));
        }
    }
}
