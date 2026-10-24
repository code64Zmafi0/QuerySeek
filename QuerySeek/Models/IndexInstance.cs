using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Runtime;
using MessagePack;

namespace QuerySeek.Models;

[MessagePackObject]
public class IndexInstance
{
    public static readonly IndexInstance Empty = new();

    /// <summary>
    /// Информация для сущностей о линках и потомках
    /// </summary>
    [Key(1)]
    public IReadOnlyDictionary<Key, EntityMeta> Entities { get; internal set; } = ImmutableDictionary<Key, EntityMeta>.Empty;

    /// <summary>
    /// ID слов по хешу нграмма
    /// </summary>
    [Key(2)]
    public IReadOnlyDictionary<int, NgrammAssociation[]> WordsIdsByNgramms { get; internal set; } = ImmutableDictionary<int, NgrammAssociation[]>.Empty;

    /// <summary>
    /// Словарь WordId -> Types -> Containers -> MatchesToEntites
    /// Так как ид слов последовательны использован массив вместо словаря - так как словарь большого размера в разы медленней на обращени и занимает больше места
    /// </summary>
    [Key(3)]
    public KeyValuePair<byte /*TypeId*/, IReadOnlyDictionary</*ContainerKey*/ Key, WordMatchMeta[]>>[][/*WordId*/] EntitiesSearchMap { get; set; } = [];

    [IgnoreMember]
    public int EntitesCount => Entities.Count;

    public int GetEntitesCount(byte type) => Entities.Keys.Count(i => i.Type == type);
}
