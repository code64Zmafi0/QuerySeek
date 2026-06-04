namespace QuerySeek.Services.Normalizing;

public interface IPhraseSplitter
{
    string[] Tokenize(string? value);
}
