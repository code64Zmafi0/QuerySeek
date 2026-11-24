namespace QuerySeek.Normalizers;

public interface ITokenizer
{
    IEnumerable<string> Tokenize(string? value);
}
