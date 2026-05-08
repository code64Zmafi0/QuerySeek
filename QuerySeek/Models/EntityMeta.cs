using MessagePack;

namespace QuerySeek.Models;

/// <summary>
/// Храним линки и потомков
/// </summary>
[MessagePackObject]
public readonly struct EntityMeta
{
    public EntityMeta() 
    {
        Links = Array.Empty<Key>();
        Childs = Array.Empty<Key>();
    }

    public EntityMeta(Key[] links)
    {
        Links = links;
        Childs = Array.Empty<Key>();
    }

    public EntityMeta(Key[] links, Key[] childs)
    {
        Links = links;
        Childs = childs;
    }

    [Key(1)]
    public readonly Key[] Links;

    [Key(2)]
    public readonly Key[] Childs;
}
