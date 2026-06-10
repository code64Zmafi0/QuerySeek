namespace QuerySeek.Services.Normalizing;

public static class TextPreprocessor
{
    public static string[] PreprocessPhrase(IPhraseSplitter splitter, INormalizer normalizer, string phrase)
        => [.. splitter.Tokenize(phrase)
            .Select(normalizer.Normalize)
            .Where(word => !string.IsNullOrWhiteSpace(word))];
}
