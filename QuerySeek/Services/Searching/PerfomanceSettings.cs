namespace QuerySeek.Services.Searching;

public record PerfomanceSettings(
    int MaxCheckingWordsCount,
    int SearchedWordsToStopProcess,
    int WordsSearchDictionaryPreallocate)
{
    public static readonly PerfomanceSettings Default = new(800, 4, 400_000);

    public static readonly PerfomanceSettings Fast = new(400, 2, 400_000);

    public Perfomancer GetPerfomancer() => new(SearchedWordsToStopProcess);
}
