# QuerySeek

Linked data text search engine.

## Features

- Fast fuzzy text search using 3-gramm words index
- Support for data hierarchy and relatedness. Designed to search for entities of different types (up to 250 types)
- Customization
- MessagePack binary serialization enabled index file saving utilities

## Documentation

### Defining entities of search
- Your search entity must implement the interface `IIndexedEntity`
    - GetKey() - Defines an entity keys
        - Use QS.Key() for create key, _entityType must be greater than 0_
    - GetNames() - Define the names by which the search will be carried out
        - Use the QS.Name() method and its overloads to specify names to search for and to define the name type for flexible sorting and scoring.
        - If you are using the extended name configuration QS.Name(string name, byte nameType), use nameType > 0
    - GetLinks() - Identify the keys that are associated with your entity (when creating an index, the entity will be a child of each element in the Links list)
    - GetSearchArea() - Define search area. For using SearchByAreas

### Build index

When performing a minor update of the library, it is recommended to update the index, as it may be incompatible with the old version.

#### Building

- Use `QS.Build()` to build Index intsnace index isntance, passing the normalizer and splitter instances to the method, as well as an enumeration entities for searching
- Also, you can get an instance of the builder using QS.GetBuilder(INormalizer normalizer, INameTokenizer nameTokenizer)
    - Call builder `AddEntity(IIndexedEntity indexedEntity)` method to add entity (multithreading is not working)
    - Call `Build(INormalizer normalizer, ITokenizer nameTokenizer, IEnumerable<IIndexedEntity> entities)` method to get IndexInstance

#### Save

- Use `QSSerializer.WriteIndex(string directoryPath, IndexInstance index)` for saving index files
- Use `QSSerializer.ReadIndex(string directoryPath)` for read index files

### Normalizing and splitting

The library provides built-in tools for normalizing and splitting a name into words for searching. Use this static objects.

- QuerySeek.Services.Normalizing.`DefaultNormalizer`.Instance
- QuerySeek.Services.Normalizing.`DefaultTokenizer`.Instance

Also, you can redefine normalization and tokenizer splitting on words for use in your cases. Implement: ITokenizer and INormalizer

### Search

1) Create `SearchContextBase`.  Inherit SearchContextBase if you need to store additional properties.
2) Create search pipeline `new SearchPipeline<TContext>`
3) Use `.AddSearchTokens` extension for add search tokens in context
4) Add search operations by using extension `.AddSearch` and pass `requestFactory` function (enumrable of requests to search) to configure your search request. Perform a search for entities up the dependency tree
    - `Search` - search current type entities
    - `SearchByAreas` - search current type entities in search area (search areas must be found in the Search block above)
    - `Select` - performs forced addition of entities of the target type based on the passed ids
    - `AppendChilds` - use to force adding entities by parent
    - `AppendChildsByAreas` - use to force adding entities by parent in areas
5) Execute pipeline by use `Execute(TContext context)` method
6) Use `BuildResult` SearchContext extension and pass fucntion to build result. Use `SearchContextBase.GetResults(byte type)` method to get results by types from context.

#### Scoring

Override `ScoreCalculatorBase<TContext>` - standard implementation for calculating an entity's score; takes into account matches from linked entities.
- Override **GetLinkedEntityMatchMultiplier(TContext context, Key currentEntity, Key linkedEntity)** for flexible scoring mathes of linked entities
- Override **GetNameTypeMultiplier(byte entityType, byte nameType)** for flexible scoring by name types
- Override **UseRules(TContext context, EntitySearchResult entity, in Span\<WordMatch> queryWordsMatches)** for use rules or additional scoring

Or implemet `IScoreCalculator<TContext>` and use your custom scorer. Use in `.AddSearch` method

### Optimizations

- If your entity cannot be found if the hierarchy parent is not found, be sure to set the IIndexedEntity.GetSearchArea method to improve performance
- If you using AdditionalRule use a static intances for smaller memory

## Example - Sound library

Define entities types

```csharp
public static class IndexedTypes
{
    public static readonly byte Track = 1;
    public static readonly byte Album = 2;
    public static readonly byte Artist = 3;
}
```

#### Create index

Define entities models, implement `IIndexedEntity`

```csharp
public class Track(int id, string name, int? albumId, int artistId) : IIndexedEntity
{
    public int Id => id;
    public int? AlbumId => albumId;
    public int ArtistId => artistId;
    public string Name => name;

    public Key GetKey() => QS.Key(IndexedTypes.Track, Id);

    public IEnumerable<Key> GetLinks()
    {
        if (AlbumId != null) yield return QS.Key(IndexedTypes.Album, AlbumId.Value);
        QS.Key(IndexedTypes.Artist, ArtistId);
    }

    public IEnumerable<Name> GetNames() => [ QS.Name(Name) ];

    public Key? GetSearchArea() => null;
}

public class Album(int id, string name, int artistId) : IIndexedEntity
{
    public int Id => id;
    public int ArtistId => artistId;
    public string Name => name;

    public Key GetKey() => QS.Key(IndexedTypes.Album, Id);

    public IEnumerable<Key> GetLinks() =>
    [
        QS.Key(IndexedTypes.Artist, ArtistId)
    ];

    public IEnumerable<Name> GetNames() => [ QS.Name(Name) ];

    public Key? GetSearchArea() => null;
}

public class Artist(int id, string name) : IIndexedEntity
{
    public int Id => id;
    public string Name => name;

    public Key GetKey() => QS.Key(IndexedTypes.Artist, Id);

    public IEnumerable<Key> GetLinks() => [];

    public IEnumerable<Name> GetNames() => [ QS.Name(Name) ];

    public Key? GetSearchArea() => null;
}
```

Create IndexInstance

```csharp
List<Artist> artists =
[
    new(1, "Linkin Park"),
    new(2, "Coldplay"),
    new(3, "Undefined"),
];

List<Album> albums =
[
    new(1, "Meteora", artistId: 1),
    new(2, "Parachutes", artistId: 2)
];

List<Track> tracks =
[
    new(1, "Numb", albumId: 1, artistId: 1),
    new(2, "Faint", albumId: 1, artistId: 1),
    new(3, "Yellow", albumId: 2, artistId: 2),
    new(4, "Shiver", albumId: 2, artistId: 2),
    new(5, "New Divide", albumId: null, artistId: 1)
];

IndexInstance index = QS.Build(DefaultNormalizer.Instance, DefaultTokenizer.Instance,
[
    .. artists,
    .. albums,
    .. tracks
]);
```

#### Save and read

- Use `QSSerializer.WriteIndex(string directoryPath, IndexInstance index)` for saving index files
- Use `QSSerializer.ReadIndex(string directoryPath)` for read index files

#### Search

Define search pipeline, execute and build result

```csharp
public Key[] Search(IndexInstance index, string query) => QS
    .SearchPipeline<SearchContextBase>()
    .AddSearchTokens()
    .AddSearch(context => //Follow the hierarchy
        [
            new Search(IndexedTypes.Artist),
            new Search(IndexedTypes.Album),
            new Search(IndexedTypes.Track)
        ])
    .Execute(new SearchContextBase(index, query))
    .BuildResult(context => context
        .GetAllResults()
        .OrderByDescending(i => i.Score)
        .Select(i => i.Key)
        .ToArray());
```

Or present by types

```csharp
    .BuildResult(context =>
    {
        EntitySearchResult[] artists = context.GetResults(IndexedTypes.Artist).OrderByDescending(i => i.Score).Select(i => i.Key).ToArray();
        EntitySearchResult[] albums = context.GetResults(IndexedTypes.Album).OrderByDescending(i => i.Score).Select(i => i.Key).ToArray();
        EntitySearchResult[] tracks = context.GetResults(IndexedTypes.Track).OrderByDescending(i => i.Score).Select(i => i.Key).ToArray();

        return (artists, albums, tracks);
    });
```