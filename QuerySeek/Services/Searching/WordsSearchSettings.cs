using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching;

/// <summary>
/// Настройки пойска схожих слов
/// </summary>
/// <param name="MaxCheckingWordsCount">Максимальное количетсво проверяемых схожих слов для слова из запроса</param>
/// <param name="SearchedWordsToStopProcess">Количетсво совпавших схожих слов для остановки поиска определнной сущности по текущему слову из запроса</param>
/// <param name="SimilarityCalculator">Калькулятор трешхолда поиска схожих слов в зависисмости от слова из запроса</param>
/// <param name="WordsSearchDictionaryPreallocate">Преаллокация словаря для поиска схожих слов</param>
public record WordsSearchSettings(
    int MaxCheckingWordsCount,
    int SearchedWordsToStopProcess,
    Func<Word, int> SimilarityCalculator,
    int WordsSearchDictionaryPreallocate = 300_000)
{
    public static readonly WordsSearchSettings Default = new(
        MaxCheckingWordsCount: 500,
        SearchedWordsToStopProcess: 6,
        SimilarityCalculator: (word) => word.IsDigit
            ? NgrammsWordsSearchHelper.CalculateDigitSimilarityTreshold(word)
            : NgrammsWordsSearchHelper.CalculateWordSimilarityTreshold(word, 0.4));

    public static readonly WordsSearchSettings Fast = new(
        MaxCheckingWordsCount: 200,
        SearchedWordsToStopProcess: 2,
        SimilarityCalculator: (word) => word.IsDigit
            ? NgrammsWordsSearchHelper.CalculateDigitSimilarityTreshold(word)
            : NgrammsWordsSearchHelper.CalculateWordSimilarityTreshold(word, 0.7));

    public WordsSearchManager GetWordsSearchManager() => new(SearchedWordsToStopProcess);
}
