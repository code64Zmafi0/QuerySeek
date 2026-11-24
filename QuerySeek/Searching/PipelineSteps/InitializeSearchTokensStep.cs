using System.Collections.Immutable;
using QuerySeek.Helpers;
using QuerySeek.Normalizers;
using QuerySeek.Searching.WordsSearch;

namespace QuerySeek.Searching.PipelineSteps;

public class InitializeSearchTokensStep<TContext>(
    INormalizer normalizer,
    ITokenizer tokenizer,
    WordsSearchSimilaritySettings wordsSearchSimilaritySettings,
    Func<TContext, AlternativesAndMultiplers>? alternativesFactory = null)
    : IPipelineStep<TContext>
    where TContext : SearchContextBase
{
    public void Execute(TContext context)
    {
        string[] splittedAndNormalizedQuery = TextPreprocessor.PreprocessText(tokenizer, normalizer, context.Query);
        context.SplittedAndNormalizedQuery = splittedAndNormalizedQuery;

        AlternativesAndMultiplers alternativesAndMultiplers = alternativesFactory is null
            ? AlternativesAndMultiplers.Empty
            : alternativesFactory.Invoke(context);

        context.SearchTokens = NgrammsWordsSearcher.CreateSearchTokensBundle(
            splittedAndNormalizedQuery,
            context.Index.WordsIdsByNgramms,
            wordsSearchSimilaritySettings,
            alternativesAndMultiplers.Alternatives,
            alternativesAndMultiplers.Multiplers);
    }
}

public record struct AlternativesAndMultiplers(IReadOnlyDictionary<string, string[]> Alternatives, IReadOnlyDictionary<string, double> Multiplers)
{
    public static readonly AlternativesAndMultiplers Empty = new(ImmutableDictionary<string, string[]>.Empty, ImmutableDictionary<string, double>.Empty);
}
