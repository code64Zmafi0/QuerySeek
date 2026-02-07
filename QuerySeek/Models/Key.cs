using MessagePack;

namespace QuerySeek.Models;

[MessagePackObject]
public readonly struct Key : IEquatable<Key>
{
    public static readonly Key Default = new(0, 0);

    public Key() { }

    public Key(byte type, int id)
    {
        Id = id;
        Type = type;
    }

    [Key(2)]
    public int Id { get; }

    [Key(1)]
    public byte Type { get; }

    public bool Equals(Key other)
        => Id == other!.Id && Type == other.Type;

    public override bool Equals(object? obj)
        => obj is Key key && Equals(key);

    public override int GetHashCode()
    {
        int num = 5381;
        int num2 = num;

        num = (num << 5) + (num ^ Type);
        num2 = (num2 << 5) + (num2 ^ Id);

        return num + num2 * 1566083941;
    }

    public static bool operator ==(Key left, Key right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Key left, Key right)
    {
        return !(left == right);
    }
}
