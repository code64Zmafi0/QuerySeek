using QuerySeek.Services.Normalizing;

namespace QuerySeek.Services.Helpers;

public static class TextPreprocessor
{
    public static string[] PreprocessText(ITokenizer nameTokenizer, INormalizer normalizer, string name)
        => [..nameTokenizer.Tokenize(normalizer.Normalize(name))];
}
