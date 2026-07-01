using Moq;
using WorshipSearchWeb.Models;
using WorshipSearchWeb.Services;
using Xunit;

namespace WorshipSearchWeb.Tests.Tests;

public class JsonSearchEngineTests
{
    // ── EditDistance ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("", "", 0)]
    [InlineData("abc", "abc", 0)]
    [InlineData("abc", "axc", 1)]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("adoracao", "adoracao", 0)]
    [InlineData("amor", "amour", 1)]
    public void EditDistance_ReturnsLevenshtein(string a, string b, int expected)
    {
        Assert.Equal(expected, JsonSearchEngine.EditDistance(a, b));
    }

    // ── Normalize ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Adoração", "adoracao")]
    [InlineData("Fé", "fe")]
    [InlineData("GRAÇA", "graca")]
    [InlineData("Louvores", "louvores")]
    [InlineData("é Ele", "e ele")]
    public void Normalize_StripsDiacriticsAndLowers(string input, string expected)
    {
        Assert.Equal(expected, JsonSearchEngine.Normalize(input));
    }

    // ── ScoreField ───────────────────────────────────────────────────────────

    [Fact]
    public void ScoreField_EmptyField_ReturnsZero()
    {
        Assert.Equal(0, JsonSearchEngine.ScoreField("", "amor", 10));
        Assert.Equal(0, JsonSearchEngine.ScoreField("   ", "amor", 10));
    }

    [Fact]
    public void ScoreField_ExactNormalizedMatch_ReturnsDoubleWeight()
    {
        // field normalizes to same as term → weight * 2
        Assert.Equal(20, JsonSearchEngine.ScoreField("Amor", "amor", 10));
    }

    [Fact]
    public void ScoreField_SubstringMatch_ReturnsWeight()
    {
        // "amor" is contained in "amor eterno"
        Assert.Equal(10, JsonSearchEngine.ScoreField("amor eterno", "amor", 10));
    }

    [Fact]
    public void ScoreField_PrefixMatch_Returns60Percent()
    {
        // "amor" starts with "amor" (prefix 4 chars) inside the word "amoroso"
        var score = JsonSearchEngine.ScoreField("amoroso", "amor", 10);
        Assert.Equal(6.0, score, precision: 5);
    }

    [Fact]
    public void ScoreField_FuzzyMatch_Returns40Percent()
    {
        // "louvor" vs "louvos" — edit distance 1, both ≥ 5 chars
        var score = JsonSearchEngine.ScoreField("louvos", "louvor", 10);
        Assert.Equal(4.0, score, precision: 5);
    }

    [Fact]
    public void ScoreField_NoMatch_ReturnsZero()
    {
        Assert.Equal(0, JsonSearchEngine.ScoreField("graça divina", "adoracao", 10));
    }

    [Fact]
    public void ScoreField_MultiWordTerm_SkipsPrefixAndFuzzy()
    {
        // Multi-word terms like "isaias 41" must not match via prefix/fuzzy — only exact/substring
        Assert.Equal(0, JsonSearchEngine.ScoreField("isaias", "isaias 41", 10));
        Assert.Equal(10, JsonSearchEngine.ScoreField("isaias 41", "isaias 41", 10));
    }

    // ── ScoreList ─────────────────────────────────────────────────────────────

    [Fact]
    public void ScoreList_NullList_ReturnsZero()
    {
        Assert.Equal(0, JsonSearchEngine.ScoreList(null, "amor", 5));
    }

    [Fact]
    public void ScoreList_AccumulatesAcrossItems()
    {
        var list = new List<string> { "adoração", "amor de Deus", "graça" };
        // "amor" matches "amor de Deus" as substring (weight 5) + "adoração" no match + "graça" no match
        var score = JsonSearchEngine.ScoreList(list, "amor", 5);
        Assert.True(score > 0);
    }

    // ── TitlePriorityBonus ───────────────────────────────────────────────────

    [Fact]
    public void TitlePriorityBonus_ExactNormalizedQueryEqualsTitle_Returns2000()
    {
        var terms = new[] { "e", "ele" };
        var bonus = JsonSearchEngine.TitlePriorityBonus("É Ele", terms, "e ele");
        Assert.Equal(2000, bonus);
    }

    [Fact]
    public void TitlePriorityBonus_AllTermsInTitle_Returns1000()
    {
        var terms = new[] { "graca", "divina" };
        var bonus = JsonSearchEngine.TitlePriorityBonus("Graça Divina", terms, "graca divina x");
        Assert.Equal(1000, bonus);
    }

    [Fact]
    public void TitlePriorityBonus_AllFuzzyMatchTitle_Returns500()
    {
        // "louvor" ≈ "louves" within edit distance 1 for len≥5
        var terms = new[] { "louvor" };
        var bonus = JsonSearchEngine.TitlePriorityBonus("Louves", terms, "louvor x");
        Assert.Equal(500, bonus);
    }

    [Fact]
    public void TitlePriorityBonus_NoMatch_ReturnsZero()
    {
        var terms = new[] { "adoracao" };
        var bonus = JsonSearchEngine.TitlePriorityBonus("Graça Divina", terms, "adoracao");
        Assert.Equal(0, bonus);
    }

    [Fact]
    public void TitlePriorityBonus_EmptyTerms_ReturnsZero()
    {
        var bonus = JsonSearchEngine.TitlePriorityBonus("Amor de Deus", Array.Empty<string>(), "");
        Assert.Equal(0, bonus);
    }

    // ── SearchAsync (integration via mocks) ──────────────────────────────────

    private static (Mock<IMusicRepository> repoMock, Mock<ISearchRestrictionsService> restrMock, JsonSearchEngine engine)
        BuildEngine(List<(string, MusicDocument)> docs)
    {
        var repoMock = new Mock<IMusicRepository>();
        repoMock.Setup(r => r.LoadAllAsync()).ReturnsAsync(docs);

        var restrMock = new Mock<ISearchRestrictionsService>();
        restrMock.Setup(r => r.LoadAsync()).ReturnsAsync(new SearchRestrictions());

        var engine = new JsonSearchEngine(repoMock.Object, restrMock.Object);
        return (repoMock, restrMock, engine);
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsAllDocs()
    {
        var docs = new List<(string, MusicDocument)>
        {
            ("a.json", new MusicDocument { Title = "Alpha" }),
            ("b.json", new MusicDocument { Title = "Beta"  }),
        };
        var (_, _, engine) = BuildEngine(docs);

        var results = await engine.SearchAsync(new SearchQuery { Query = "", Page = 1, PageSize = 20 });
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task SearchAsync_QueryMatchesTitle_ReturnsMatchedFirst()
    {
        var docs = new List<(string, MusicDocument)>
        {
            ("a.json", new MusicDocument { Title = "Graça Divina"  }),
            ("b.json", new MusicDocument { Title = "Santo Espírito" }),
        };
        var (_, _, engine) = BuildEngine(docs);

        var results = await engine.SearchAsync(new SearchQuery { Query = "graca", Page = 1, PageSize = 20 });
        Assert.Single(results);
        Assert.Equal("Graça Divina", results[0].Document.Title);
    }

    [Fact]
    public async Task SearchAsync_ApprovedOnly_FiltersUnapproved()
    {
        var docs = new List<(string, MusicDocument)>
        {
            ("a.json", new MusicDocument { Title = "Aprovada", Approved = true  }),
            ("b.json", new MusicDocument { Title = "Pendente",  Approved = false }),
        };
        var (_, _, engine) = BuildEngine(docs);

        var results = await engine.SearchAsync(new SearchQuery { Query = "", ApprovedOnly = true, Page = 1, PageSize = 20 });
        Assert.Single(results);
        Assert.Equal("Aprovada", results[0].Document.Title);
    }

    [Fact]
    public async Task SearchAsync_ExcludedGenre_IsFiltered()
    {
        var docs = new List<(string, MusicDocument)>
        {
            ("a.json", new MusicDocument { Title = "Rock", Genre = "rock" }),
            ("b.json", new MusicDocument { Title = "Worship" }),
        };
        var repoMock  = new Mock<IMusicRepository>();
        var restrMock = new Mock<ISearchRestrictionsService>();
        repoMock.Setup(r => r.LoadAllAsync()).ReturnsAsync(docs);
        restrMock.Setup(r => r.LoadAsync()).ReturnsAsync(new SearchRestrictions { Excluded = new() { "rock" } });

        var engine = new JsonSearchEngine(repoMock.Object, restrMock.Object);
        var results = await engine.SearchAsync(new SearchQuery { Query = "", Page = 1, PageSize = 20 });
        Assert.Single(results);
        Assert.Equal("Worship", results[0].Document.Title);
    }

    [Fact]
    public async Task SearchAsync_TitleExactMatch_RanksAboveThemeMatch()
    {
        var docs = new List<(string, MusicDocument)>
        {
            ("a.json", new MusicDocument
            {
                Title  = "Aleluia",
                Search = new SearchMetadata { Themes = new() { "louvor" } }
            }),
            ("b.json", new MusicDocument
            {
                Title  = "Canto de Louvor",
                Search = new SearchMetadata { Themes = new() { "adoracao" } }
            }),
        };
        var (_, _, engine) = BuildEngine(docs);

        // Query "louvor" should match "Canto de Louvor" (title) AND "Aleluia" (theme)
        // Title match should score higher
        var results = await engine.SearchAsync(new SearchQuery { Query = "louvor", Page = 1, PageSize = 20 });
        Assert.Equal(2, results.Count);
        Assert.Equal("Canto de Louvor", results[0].Document.Title);
    }

    [Fact]
    public async Task SearchAsync_DiacriticFreeQuery_MatchesDiacriticTitle()
    {
        var docs = new List<(string, MusicDocument)>
        {
            ("a.json", new MusicDocument { Title = "Adoração" }),
        };
        var (_, _, engine) = BuildEngine(docs);

        var results = await engine.SearchAsync(new SearchQuery { Query = "adoracao", Page = 1, PageSize = 20 });
        Assert.Single(results);
    }

    // ── SearchByDocumentAsync ────────────────────────────────────────────────

    [Fact]
    public async Task SearchByDocumentAsync_WorshipMomentCriterion_MatchesCorrectly()
    {
        var docs = new List<(string, MusicDocument)>
        {
            ("a.json", new MusicDocument
            {
                Title  = "Ofertório",
                Search = new SearchMetadata { WorshipMoments = new() { "ofertório", "coleta" } }
            }),
            ("b.json", new MusicDocument
            {
                Title  = "Comunhão",
                Search = new SearchMetadata { WorshipMoments = new() { "comunhão" } }
            }),
        };
        var (_, _, engine) = BuildEngine(docs);

        var criteria = new List<SearchCriterion>
        {
            new()
            {
                Input = "ofertório",
                Type  = "worship_moment",
                Tags  = new SearchMetadata { WorshipMoments = new() { "ofertório" } }
            }
        };

        var results = await engine.SearchByDocumentAsync(criteria, false);
        Assert.Single(results);
        Assert.Equal("Ofertório", results[0].Document.Title);
    }

    [Fact]
    public async Task SearchByDocumentAsync_BiblicalReferenceCriterion_MatchesReferences()
    {
        var docs = new List<(string, MusicDocument)>
        {
            ("a.json", new MusicDocument
            {
                Title  = "Salmo 91",
                Search = new SearchMetadata { BiblicalReferences = new() { "Salmo 91" } }
            }),
            ("b.json", new MusicDocument
            {
                Title  = "Amor Eterno",
                Search = new SearchMetadata { Themes = new() { "amor" } }
            }),
        };
        var (_, _, engine) = BuildEngine(docs);

        var criteria = new List<SearchCriterion>
        {
            new()
            {
                Input = "Salmo 91",
                Type  = "biblical_reference",
                Tags  = new SearchMetadata { BiblicalReferences = new() { "Salmo 91" } }
            }
        };

        var results = await engine.SearchByDocumentAsync(criteria, false);
        Assert.Single(results);
        Assert.Equal("Salmo 91", results[0].Document.Title);
    }

    [Fact]
    public async Task SearchByDocumentAsync_MultipleCriteria_RanksByMatchCount()
    {
        var docs = new List<(string, MusicDocument)>
        {
            ("a.json", new MusicDocument
            {
                Title  = "Louvor Profético",
                Search = new SearchMetadata
                {
                    Themes        = new() { "louvor" },
                    WorshipMoments = new() { "adoração" }
                }
            }),
            ("b.json", new MusicDocument
            {
                Title  = "Hino de Amor",
                Search = new SearchMetadata { Themes = new() { "louvor" } }
            }),
        };
        var (_, _, engine) = BuildEngine(docs);

        var criteria = new List<SearchCriterion>
        {
            new() { Input = "louvor",   Type = "theme",          Tags = new SearchMetadata { Themes = new() { "louvor" } } },
            new() { Input = "adoração", Type = "worship_moment", Tags = new SearchMetadata { WorshipMoments = new() { "adoração" } } },
        };

        var results = await engine.SearchByDocumentAsync(criteria, false);
        Assert.Equal(2, results.Count);
        // "Louvor Profético" matches both criteria → more MatchedTerms → first
        Assert.Equal("Louvor Profético", results[0].Document.Title);
    }

    [Fact]
    public async Task CountAsync_ReturnsMatchingCount()
    {
        var docs = new List<(string, MusicDocument)>
        {
            ("a.json", new MusicDocument { Title = "Amor de Deus" }),
            ("b.json", new MusicDocument { Title = "Fé e Esperança" }),
            ("c.json", new MusicDocument { Title = "Graça" }),
        };
        var (_, _, engine) = BuildEngine(docs);

        var count = await engine.CountAsync(new SearchQuery { Query = "amor", Page = 1, PageSize = 20 });
        Assert.Equal(1, count);
    }
}
