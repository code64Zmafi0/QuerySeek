namespace QuerySeek.Services.Normalizing;

public interface INameTokenizer
{
    IEnumerable<string> Tokenize(string? value);
}
