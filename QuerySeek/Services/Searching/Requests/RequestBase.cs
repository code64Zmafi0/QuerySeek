namespace QuerySeek.Services.Searching.Requests;

public abstract class RequestBase(byte targetType)
{
    public byte TargetType { get; } = targetType;

    /// <summary>
    /// Выполняет процесс поиска в индексе по заданному запросу и заполняет результат в searchContext
    /// </summary>
    /// <param name="searchContext"></param>
    /// <param name="wordsBundle"></param>
    /// <param name="perfomance"></param>
    /// <param name="ct"></param>
    public abstract void ProcessRequest(
        SearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        PerfomanceSettings perfomance,
        CancellationToken ct);
}
