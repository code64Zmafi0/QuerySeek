using QuerySeek.Models;
using QuerySeek.Services.Extensions;

namespace QuerySeek.Interfaces;

public interface IIndexedEntity
{
    /// <summary>
    /// Entity key.
    /// </summary>
    Key GetKey();

    /// <summary>
    /// Entity names.
    /// </summary>
    IEnumerable<Phrase> GetNames();

    /// <summary>
    /// Components of entity.
    /// </summary>
    IEnumerable<Key> GetLinks();

    /// <summary>
    /// The container to which the entity is bound. For using SearchByContainer request.
    /// </summary>
    Key? GetContainer();

    /// <summary>
    /// Parents to bind to. For using AppendChilds request.
    /// </summary>
    IEnumerable<Key> GetParents();
}
