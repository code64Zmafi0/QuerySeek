namespace QuerySeek.Services.Normalizing;

public interface ITokenizer
{
    IEnumerable<string> Tokenize(string? value);
}
