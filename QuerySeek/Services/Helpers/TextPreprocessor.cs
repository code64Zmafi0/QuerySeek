using QuerySeek.Services.Normalizing;

namespace QuerySeek.Services.Helpers;

public static class TextPreprocessor
{
    public static string[] PreprocessName(INameTokenizer nameTokenizer, INormalizer normalizer, string name)
        => [..nameTokenizer.Tokenize(normalizer.Normalize(name))];
}
