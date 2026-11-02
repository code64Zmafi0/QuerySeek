# QuerySeek

Linked data text search engine.

## Features

- Fast text search using 3-gramm words index
- Designed to search for entities of different types (up to 250 types)
- Support for data hierarchy and relatedness
- Flexible sorting settings
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

- Use `QS.WriteIndex(IndexInstance index, string filePath)` for saving index file
- Use `QS.ReadIndex(string filePath)` for read index file

### Normalizing and splitting

The library provides built-in tools for normalizing and splitting a name into words for searching. Use this static objects.

QuerySeek.Services.Normalizing.`DefaultNormalizer`.Instance
QuerySeek.Services.Normalizing.`DefaultTokenizer`.Instance

Also, you can redefine normalization and tokenizer splitting on words for use in your cases. Implement: ITokenizer and INormalizer

### Scoring

Override `ScoreCalculatorBase<TContext>` - standard implementation for calculating an entity's score; takes into account matches from linked entities.
- Override **GetLinkedEntityMatchMultiplier(TContext context, Key currentEntity, Key linkedEntity)** for flexible scoring mathes of linked entities
- Override **GetNameTypeMultiplier(byte entityType, byte nameType)** for flexible scoring by name types
- Override **OnEntityMatched(TContext context, EntitySearchResult entity, in Span\<WordMatch> queryWordsMatches)** for use rules or additional scoring

Or implemet `IScoreCalculator<TContext>` and use your custom scorer

### Search

Inherit `SearchContextBase` if you need to store additional sorting properties

Implement `SearcherBase<TContext, TResult>` 
- Implement **GetRequest(TContext context)** method (enumrable of requests to search) to configure your search request. The query options are presented below (perform a search for entities up the dependency tree)
    - `Search` - search current type entities
    - `SearchByAreas` - search current type entities in search area (search areas must be found in the Search block above)
    - `Select` - performs forced addition of entities of the target type based on the passed ids
    - `AppendChilds` - use to force adding entities by parent
    - `AppendChildsByAreas` - use to force adding entities by parent in areas
- Implement **BuildResult(TContext context)** select results from context and sorting. For sorting, use EntitySearchResult.Score property for ranging entites by matches factor or ScoreWithRules for ranging by score and rules
- Override **GetWordsSearchSettings(TContext context)** for configure words search and add alternatives

Pass `INormalizer`, `ITokenizer`, and `IScoreCalculator` to the constructor.

## Optimizations

- After deserialize index instance use indexInstance.Initialzie() method
- If your entity cannot be found if the hierarchy parent is not found, be sure to set the IIndexedEntity.GetSearchArea method to improve performance
- If you using AdditionalRule use a static intances for smaller memory 