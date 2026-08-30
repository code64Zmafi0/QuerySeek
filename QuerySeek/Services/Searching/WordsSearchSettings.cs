using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching;

/// <summary>
/// Настройки пойска схожих слов
/// </summary>
/// <param name="MaxCheckingWordsCount">Максимальное количетсво проверяемых схожих слов для слова из запроса</param>
/// <param name="WordsToStopProcessCalculator">Количетсво совпавших схожих слов для остановки поиска определнной сущности по текущему слову из запроса</param>
/// <param name="SimilarityTresholdCalculator">Калькулятор трешхолда поиска схожих слов в зависисмости от слова из запроса</param>
/// <param name="WordsSearchDictionaryPreallocate">Преаллокация словаря для поиска схожих слов</param>
public record WordsSearchSettings(
    int MaxCheckingWordsCount,
    Func<Word, int> WordsToStopProcessCalculator,
    Func<Word, int> SimilarityTresholdCalculator,
    int WordsSearchDictionaryPreallocate = 300_000)
{
    public static readonly WordsSearchSettings Default = new(
        MaxCheckingWordsCount: 500,
        WordsToStopProcessCalculator: (word) => 30,
        SimilarityTresholdCalculator: (word) => word.IsDigit
            ? NgrammsWordsSearchHelper.CalculateDigitSimilarityTreshold(word)
            : NgrammsWordsSearchHelper.CalculateWordSimilarityTreshold(word, 0.4));

    public static readonly WordsSearchSettings Fast = new(
        MaxCheckingWordsCount: 200,
        WordsToStopProcessCalculator: (_) => 10,
        SimilarityTresholdCalculator: (word) => word.IsDigit
            ? NgrammsWordsSearchHelper.CalculateDigitSimilarityTreshold(word)
            : NgrammsWordsSearchHelper.CalculateWordSimilarityTreshold(word, 0.7));

    public WordsSearchStopManager GetWordsSearchStopManager(Word word) => new(WordsToStopProcessCalculator(word));
}
