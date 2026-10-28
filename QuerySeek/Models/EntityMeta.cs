using MessagePack;
using QuerySeek.Serialization;

namespace QuerySeek.Models;

/// <summary>
/// Отвечает за хранение линков и потомков
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
    [OptimizeArrayEmpty(typeof(Key[]))]
    public Key[] Links { get; set; } = Array.Empty<Key>();

    [Key(2)]
    [OptimizeArrayEmpty(typeof(Key[]))]
    public Key[] Childs { get; set; } = Array.Empty<Key>();
}
