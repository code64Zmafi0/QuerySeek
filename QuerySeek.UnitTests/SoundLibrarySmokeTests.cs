using QuerySeek.Models;
using QuerySeek.Services.Building;
using QuerySeek.Services.Extensions;
using QuerySeek.Services.Normalizing;
using QuerySeek.Services.Searching;
using QuerySeek.Services.Searching.Selection;

namespace QuerySeek.UnitTests;

[TestFixture]
public class SoundLibrarySmokeTests
{
    private IndexInstance _instance = null!;

    [OneTimeSetUp]
    public void Setup()
    {
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
            new(5, "New Divide", albumId: null, artistId: 3),
            new(6, "New Dream", albumId: null, artistId: 3),
            new(7, "Duo", albumId: null, artistId: 1),
            new(8, "Duo", albumId: null, artistId: 2),
        ];

        _instance = QS.Build(DefaultNormalizer.Instance, DefaultTokenizer.Instance,
        [
            .. artists,
            .. albums,
            .. tracks
        ]);
    }

    private static IEnumerable<TestCaseData> SearchCases()
    {
        yield return new TestCaseData("Numb")
            .Returns(new[] { QS.Key(IndexedTypes.Track, 1) })
            .SetName("Search_ExactTrackName_ReturnsTrack");

        yield return new TestCaseData("Divide")
            .Returns(new[] { QS.Key(IndexedTypes.Track, 5) })
            .SetName("Search_TrackWithoutAlbum_ReturnsTrack");

        yield return new TestCaseData("Meteora")
            .Returns(new[] { QS.Key(IndexedTypes.Album, 1) })
            .SetName("Search_AlbumName_ReturnsAlbum");

        yield return new TestCaseData("Coldplay")
            .Returns(new[] { QS.Key(IndexedTypes.Artist, 2) })
            .SetName("Search_ArtistName_ReturnsArtist");

        yield return new TestCaseData("Parochttes")
            .Returns(new[] { QS.Key(IndexedTypes.Album, 2) })
            .SetName("Search_Fuzzy_ReturnsAlbum");

        yield return new TestCaseData("new")
            .Returns((new[] { QS.Key(IndexedTypes.Track, 5), QS.Key(IndexedTypes.Track, 6) }))
            .SetName("Search_PartialName_ReturnsTwoTracks");

        yield return new TestCaseData("Duo park")
            .Returns((new[] { QS.Key(IndexedTypes.Track, 7), QS.Key(IndexedTypes.Artist, 1), QS.Key(IndexedTypes.Track, 8), QS.Key(IndexedTypes.Album, 2) }))
            .SetName("Search_TrackAndArtist_ReturnsRangedTracks");

        yield return new TestCaseData("Duo cold")
            .Returns((new[] { QS.Key(IndexedTypes.Track, 8), QS.Key(IndexedTypes.Track, 7), QS.Key(IndexedTypes.Artist, 2) }))
            .SetName("Search_TrackAndArtist_ReturnsRangedTracks");

        yield return new TestCaseData("UnknownSong")
            .Returns(Array.Empty<Key>())
            .SetName("Search_NonExistent_ReturnsEmpty");
    }

    [TestCaseSource(nameof(SearchCases))]
    public Key[] Search_ByQuery_ReturnsExpectedKeys(string query) => QS
        .SearchPipeline<SearchContextBase>()
        .AddSearchTokens()
        .AddSearch(context =>
            [
                new Search(IndexedTypes.Artist),
                new Search(IndexedTypes.Album),
                new Search(IndexedTypes.Track)
            ])
        .Execute(new SearchContextBase(_instance, query))
        .BuildResult(context => context
            .GetAllResults()
            .OrderByDescending(i => i.Score)
            .Select(i => i.Key)
            .ToArray());
}

public static class IndexedTypes
{
    public static readonly byte Track = 1;
    public static readonly byte Album = 2;
    public static readonly byte Artist = 3;
}

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
        yield return QS.Key(IndexedTypes.Artist, ArtistId);
    }

    public IEnumerable<Name> GetNames() => [QS.Name(Name)];

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

    public IEnumerable<Name> GetNames() => [QS.Name(Name)];

    public Key? GetSearchArea() => null;
}

public class Artist(int id, string name) : IIndexedEntity
{
    public int Id => id;
    public string Name => name;

    public Key GetKey() => QS.Key(IndexedTypes.Artist, Id);

    public IEnumerable<Key> GetLinks() => [];

    public IEnumerable<Name> GetNames() => [QS.Name(Name)];

    public Key? GetSearchArea() => null;
}
