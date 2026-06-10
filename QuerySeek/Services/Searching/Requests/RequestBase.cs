namespace QuerySeek.Services.Searching.Requests;

public abstract class RequestBase(byte targetType)
{
    public byte TargetType { get; } = targetType;

    /// <summary>
    /// Выполняет процесс поиска сущностей в индексе и заполняет результат в SearchContext
    /// </summary>
    /// <param name="searchContext"></param>
    /// <param name="ct"></param>
    public abstract void ProcessRequest(SearchContextBase searchContext, CancellationToken ct);
}
