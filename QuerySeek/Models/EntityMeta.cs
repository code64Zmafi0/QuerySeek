using MessagePack;

namespace QuerySeek.Models;

/// <summary>
/// Храним линки и потомков
/// </summary>
[MessagePackObject]
public class EntityMeta
{
    public EntityMeta() { }

    public EntityMeta(Key[] links)
    {
        Links = links;
        Childs = Array.Empty<Key>();
    }

    [Key(1)]
    public Key[] Links { get; internal set; } = Array.Empty<Key>();

    [Key(2)]
    public Key[] Childs { get; internal set; } = Array.Empty<Key>();
}
