using MessagePack;

namespace QuerySeek.Models;

/// <summary>
/// Отвечает за хранение линков и потомков
/// </summary>
[MessagePackObject]
public class EntityMeta
{
    private Key[] _links = Array.Empty<Key>();
    private Key[] _childs = Array.Empty<Key>();

    public EntityMeta() { }

    public EntityMeta(Key[] links)
    {
        Links = links;
        Childs = Array.Empty<Key>();
    }

    [Key(1)]
    public Key[] Links
    {
        get => _links;
        set => _links = value.Length == 0 
            ? Array.Empty<Key>()
            : value;
    }

    [Key(2)]
    public Key[] Childs
    {
        get => _childs;
        set => _childs = value.Length == 0
            ? Array.Empty<Key>()
            : value;
    }
}
