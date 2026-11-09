using System.Collections.Immutable;

namespace QuerySeek.Models;

public class IndexInstance
{
    public static readonly IndexInstance Empty = new();

    /// <summary>
    /// Информация для сущностей о линках и потомках
    /// </summary>
    public IReadOnlyDictionary<Key, EntityMeta> Entities { get; internal set; } = ImmutableDictionary<Key, EntityMeta>.Empty;

    /// <summary>
    /// ID слов по хешу нграмма
    /// </summary>
    public IReadOnlyDictionary<int, NgrammAssociation[]> WordsIdsByNgramms { get; internal set; } = ImmutableDictionary<int, NgrammAssociation[]>.Empty;

    /// <summary>
    /// Словарь WordId -> Types -> Containers -> MatchesToEntites
    /// Так как ид слов последовательны использован массив вместо словаря - так как словарь большого размера в разы медленней на обращени и занимает больше места
    /// </summary>
    public KeyValuePair<byte /*TypeId*/, IReadOnlyDictionary</*ContainerKey*/ Key, WordMatchMeta[]>>[][/*WordId*/] EntitiesSearchMap { get; set; } = [];

    public int EntitesCount => Entities.Count;

    public int GetEntitesCount(byte type) => Entities.Keys.Count(i => i.Type == type);
}
