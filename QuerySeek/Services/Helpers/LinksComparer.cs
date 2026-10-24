using QuerySeek.Models;

namespace QuerySeek.Services.Helpers;

public class LinksComparer : IEqualityComparer<Key[]>
{
    public bool Equals(Key[]? x, Key[]? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        if (x.Length != y.Length) return false;

        for (int i = 0; i < x.Length; i++)
        {
            if (!x[i].Equals(y[i])) return false;
        }

        return true;
    }

    public int GetHashCode(Key[]? obj)
    {
        if (obj is null) return 0;

        var hashCode = new HashCode();

        foreach (var item in obj)
        {
            hashCode.Add(item);
        }

        return hashCode.ToHashCode();
    }
}
