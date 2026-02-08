namespace QuerySeek.Services.Searching;

public record PerfomanceSettings(
    int MaxCheckingWordsCount,
    int SearchedWordsToStopProcess,
    int WordsSearchDictionaryPreallocate)
{
    public static readonly PerfomanceSettings Default = new(1000, 4, 400_000);

    public static readonly PerfomanceSettings Fast = new(500, 2, 400_000);

    public Perfomancer GetPerfomancer()
        => new(SearchedWordsToStopProcess);
}
