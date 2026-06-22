using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching.Models;

public record WordsSearchSettings(
    int MaxCheckingWordsCount,
    int SearchedWordsToStopProcess,
    int WordsSearchDictionaryPreallocate = 300_000,
    double Similarity = 0.5)
{
    public static readonly WordsSearchSettings Default = new(
        MaxCheckingWordsCount: 600,
        SearchedWordsToStopProcess: 6);

    public static readonly WordsSearchSettings Fast = new(
        MaxCheckingWordsCount: 200,
        SearchedWordsToStopProcess: 2,
        Similarity: 0.7);

    public WordsSearchManager GetWordsSearchManager() => new(SearchedWordsToStopProcess);
}
