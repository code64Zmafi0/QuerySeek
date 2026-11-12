using QuerySeek.Services.Normalizing;

namespace QuerySeek.Services.Helpers;

public static class TextPreprocessor
{
    public static string[] PreprocessText(ITokenizer tokenizer, INormalizer normalizer, string text)
        => [..tokenizer.Tokenize(normalizer.Normalize(text))];
}
