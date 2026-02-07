namespace QuerySeek.Services.Searching;

public record PerfomanceSettings(
    int MaxCheckingWordsCount,
    int SearchedWordsToStopProcess)
{
    public static readonly PerfomanceSettings Default = new(1000, 4);

    public static readonly PerfomanceSettings Fast = new(500, 2);

    public Perfomancer GetPerfomancer()
        => new(SearchedWordsToStopProcess);
}
