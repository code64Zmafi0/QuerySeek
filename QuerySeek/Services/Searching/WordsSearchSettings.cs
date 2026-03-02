namespace QuerySeek.Services.Searching;

public record WordsSearchSettings(
    int MaxCheckingWordsCount,
    int SearchedWordsToStopProcess,
    int WordsSearchDictionaryPreallocate,
    double Similarity = 0.5)
{
    public static readonly WordsSearchSettings Default = new(500, 6, 350_000);

    public static readonly WordsSearchSettings Fast = new(200, 2, 200_000);

    public WordsSearchManager GetWordsSearchManager() => new(SearchedWordsToStopProcess);
}
