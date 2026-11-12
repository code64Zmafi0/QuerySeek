using QuerySeek.Models;

namespace QuerySeek.Services.Building;

public interface IIndexedEntity
{
    /// <summary>
    /// Entity key
    /// </summary>
    Key GetKey();

    /// <summary>
    /// Entity names
    /// </summary>
    IEnumerable<Name> GetNames();

    /// <summary>
    /// Components of entity
    /// </summary>
    IEnumerable<Key> GetLinks();

    /// <summary>
    /// The search area to which the entity is bound. For using SearchByAreas request
    /// </summary>
    Key? GetSearchArea();
}
