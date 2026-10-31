using MessagePack;

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

    /// <summary>
    /// Колликция привязанных сущностей
    /// </summary>
    [Key(1)]
    public Key[] Links { get; set; } = Array.Empty<Key>();

    /// <summary>
    /// Коллекция дочерних сущностей
    /// </summary>
    [Key(2)]
    public Key[] Childs { get; set; } = Array.Empty<Key>();

    /// <summary>
    /// Количество уникальных слов
    /// </summary>
    [Key(3)]
    public int UniqWordsCount { get; internal set; } = 0;
}
