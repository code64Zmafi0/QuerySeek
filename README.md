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
- Your search entity must implement the interface **IIndexedEntity**
    - GetKey() - Defines an entity keys
        - Use QS.Key() for create key, _entityType must be greater than 0_
    - GetNames() - Define the names by which the search will be carried out
        - Use the _QS.Name()_ method and its overloads to specify names to search for and to define the name type for flexible sorting and scoring.
        - If you are using the extended name configuration QS.Name(string name, byte nameType), use nameType > 0
    - GetLinks() - Identify the keys that are associated with your entity (when creating an index, the entity will be a child of each element in the Links list)
    - GetSearchArea() - Define search area. For using SearchByAreas

### Build index

**Building**

- Use QS.Build() to build Index intsnace index isntance, passing the normalizer and splitter instances to the method, as well as an enumeration entities for searching
- Also, you can get an instance of the builder using QS.GetBuilder(INormalizer normalizer, INameTokenizer nameTokenizer)
    - Call builder _AddEntity_ method to add entity (multithreading is not working)
    - Call _Build_ to get IndexInstance

**Save**

- Use QS.WriteIndex(IndexInstance index, string filePath) for saving index file
- Use QS.ReadIndex(string filePath) for read index file

### Normalizing and splitting
The library provides built-in tools for normalizing and splitting a name into words for searching. Use this static objects.

QuerySeek.Services.Normalizing.DefaultNormalizer.Instance
QuerySeek.Services.Normalizing.DefaultNameTokenizer.Instance

Also, you can redefine normalization and tokenizer splitting on words for use in your cases. Implement: INameTokenizer and INormalizer

### Search

*Override SearchContextBase if you need to store additional sorting properties* 


*Implement SearcherBase* 
- Implement SearcherBase.Request property (array of requests to search) to configure your search request. The query options are presented below (use in the same order for proper operation)
    - Search - search current type entities
    - SearchByAres - search current type entities in search area (search areas must be found in the Search block above)
    - Select - performs forced addition of entities of the target type based on the passed ids
    - AppendChilds - use to force adding entities by parent
    - AppendChildsByAreas - use to force adding entities by parent in areas
- Override GetLinkedEntityMatchMiltipler(byte entityType, byte linkedType) for flexible scoring mathes of linked entities
- Override GetNameTypeMultipler(byte nameType) for flexible scoring by name types
- Override TypeBundlePreprocessing(TContext context, byte type, IEnumerable<EntitySearchResult> result) to add rules or filter the results that will be output as a result
- Override Ranging(TContext context, IOrderedEnumerable<EntitySearchResult> result) for sorting, use EntitySearchResult.Score property for ranging entites

**Search**

- Use SearcherBase method Search passing the search context
- Or use SearcherBase method SearchTypes passing the search context and list of target entities with their count


## Optimizations

- If your entity cannot be found if the hierarchy parent is not found, be sure to set the IIndexedEntity.GetSearchArea method to improve performance.
- If you using AdditionalRule use a static intances for smaller memory 