namespace QuerySeek.Services.Searching;

public record WordsSearchSettings(
    int MaxCheckingWordsCount,
    int SearchedWordsToStopProcess,
    int WordsSearchDictionaryPreallocate = 350_000,
    double Similarity = 0.5)
{
    public static readonly WordsSearchSettings Default = new(600, 6);

    public static readonly WordsSearchSettings Fast = new(200, 2, 300_000, 0.7);

    public WordsSearchManager GetWordsSearchManager() => new(SearchedWordsToStopProcess);
}
