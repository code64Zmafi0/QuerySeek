using System.Collections.Immutable;
using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching.WordsSearch;

public readonly record struct SimilarityTreshold(int MinMatchedNgrammsCount, int MaxMisses);

public readonly record struct WordNgrammSearchState(
    byte Matches,
    byte Misses,
    byte LastQueryWordNgrammPosition,
    byte LastIndexWordNgrammPosition);

/// <summary>
/// Настройки поиска слов
/// </summary>
/// <param name="Settings">Настройки пойска схожих слов</param>
/// <param name="Alternatives">Возможные альтернативы для слов из запроса</param>
/// <param name="Multiplers">Множители для слов из запроса</param>
public record WordsSearchSettings(
    WordsSearchSimilaritySettings Settings,
    IReadOnlyDictionary<string, string[]> Alternatives,
    IReadOnlyDictionary<string, double> Multiplers)
{
    public static readonly WordsSearchSettings Default = new(
        WordsSearchSimilaritySettings.Default,
        ImmutableDictionary<string, string[]>.Empty,
        ImmutableDictionary<string, double>.Empty);
}

/// <summary>
/// Настройки пойска схожих слов
/// </summary>
/// <param name="MaxCheckingWordsCount">Максимальное количетсво проверяемых схожих слов для слова из запроса</param>
/// <param name="WordsToStopProcessCalculator">Количество совпавших схожих слов для остановки поиска определнной сущности по текущему слову из запроса</param>
/// <param name="SimilarityTresholdCalculator">Калькулятор трешхолда поиска схожих слов в зависисмости от слова из запроса</param>
/// <param name="WordScoreCalculator">Определяет как просчитывается скор совпадения слов по данным из совпавших нграмм</param>
/// <param name="AlternativesCount">Сколько взять похожих для каждого альтернативного слова</param>
/// <param name="WordsSearchDictionaryPreallocate">Преаллокация словаря для поиска схожих слов</param>
public record WordsSearchSimilaritySettings(
    Func<Word, int> MaxCheckingWordsCount,
    Func<Word, int> WordsToStopProcessCalculator,
    Func<Word, SimilarityTreshold> SimilarityTresholdCalculator,
    Func<Word, WordNgrammSearchState, byte> WordScoreCalculator,
    int AlternativesCount = 1,
    int WordsSearchDictionaryPreallocate = 300_000)
{
    public static readonly WordsSearchSimilaritySettings Default = new(
        MaxCheckingWordsCount: (word) =>
            word.WordText.Length < 4 ? 1000 : 500,
        WordsToStopProcessCalculator: (word) =>
            35,
        SimilarityTresholdCalculator: (word) => word.IsDigit
            ? NgrammsWordsSearcher.CalculateDigitSimilarityTreshold(word)
            : NgrammsWordsSearcher.CalculateWordSimilarityTreshold(word, 0.4),
        WordScoreCalculator: (word, searchState) =>
            (byte)(searchState.Matches - Math.Round((searchState.Misses) * 0.6)));

    public WordsSearchStopManager GetWordsSearchStopManager(Word word) => new(WordsToStopProcessCalculator(word));
}
