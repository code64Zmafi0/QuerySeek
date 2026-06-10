namespace QuerySeek.Services.Normalizing;

public interface IPhraseSplitter
{
    IEnumerable<string> Tokenize(string? value);
}
