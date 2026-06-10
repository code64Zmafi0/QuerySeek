namespace QuerySeek.Services.Building;

public class StringArraySequenceComparer : IEqualityComparer<string[]>
{
    private readonly StringComparer _stringComparer = StringComparer.Ordinal;

    public bool Equals(string[]? x, string[]? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x == null || y == null) return false;
        if (x.Length != y.Length) return false;

        return x.SequenceEqual(y, _stringComparer);
    }

    public int GetHashCode(string[] obj)
    {
        if (obj == null) return 0;

        var hash = new HashCode();
        foreach (var str in obj)
        {
            hash.Add(str, _stringComparer);
        }
        return hash.ToHashCode();
    }
}
