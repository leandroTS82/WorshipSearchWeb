using Moq;
using WorshipSearchWeb.Services;
using Xunit;

namespace WorshipSearchWeb.Tests.Tests;

public class SearchIndexClassifierTests
{
    // ── NormalizeSimple ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("Isaías", "isaias")]
    [InlineData("João",   "joao")]
    [InlineData("Salmo",  "salmo")]
    [InlineData("ÊXODO",  "exodo")]
    public void NormalizeSimple_StripsDiacriticsAndLowers(string input, string expected)
    {
        Assert.Equal(expected, SearchIndexClassifierService.NormalizeSimple(input));
    }

    // ── ApplyLocalOverride — chapter:verse pattern ───────────────────────────

    [Theory]
    [InlineData("João 3:16",   "general",  "biblical_reference")]
    [InlineData("Isaías 41:10","general",  "biblical_reference")]
    [InlineData("Salmo 23:1",  "theme",    "biblical_reference")]
    [InlineData("1 Samuel 3:10","general", "biblical_reference")]
    public void ApplyLocalOverride_ChapterVerse_AlwaysBiblical(string input, string llmType, string expected)
    {
        Assert.Equal(expected, SearchIndexClassifierService.ApplyLocalOverride(input, llmType));
    }

    // ── ApplyLocalOverride — book+chapter pattern ────────────────────────────

    [Theory]
    [InlineData("Salmo 91",    "general",  "biblical_reference")]
    [InlineData("Isaías 41",   "theme",    "biblical_reference")]
    [InlineData("1 Samuel 3",  "general",  "biblical_reference")]
    [InlineData("2 Reis 4",    "general",  "biblical_reference")]
    [InlineData("Apocalipse 1","general",  "biblical_reference")]
    public void ApplyLocalOverride_BookPlusChapter_AlwaysBiblical(string input, string llmType, string expected)
    {
        Assert.Equal(expected, SearchIndexClassifierService.ApplyLocalOverride(input, llmType));
    }

    // ── ApplyLocalOverride — LLM result passes through for non-biblical ──────

    [Theory]
    [InlineData("adoração",    "mood",    "mood")]
    [InlineData("louvor",      "theme",   "theme")]
    [InlineData("comunhão",    "context", "context")]
    [InlineData("ofertório",   "worship_moment", "worship_moment")]
    [InlineData("graça divina","general", "general")]
    public void ApplyLocalOverride_NonBiblical_ReturnsLlmType(string input, string llmType, string expected)
    {
        Assert.Equal(expected, SearchIndexClassifierService.ApplyLocalOverride(input, llmType));
    }

    // ── ApplyLocalOverride — false positive guard ────────────────────────────

    [Theory]
    [InlineData("1 amor",      "general")] // "1" + word that isn't a book
    [InlineData("Samuel",      "general")] // book name alone, no number
    [InlineData("Salmo",       "theme")]   // no chapter number after book
    [InlineData("3 palavras",  "general")] // number + word but not a book
    public void ApplyLocalOverride_FalsePositives_DoNotOverride(string input, string llmType)
    {
        Assert.Equal(llmType, SearchIndexClassifierService.ApplyLocalOverride(input, llmType));
    }

    // ── ClassifyAsync — integration through public method ────────────────────

    [Fact]
    public async Task ClassifyAsync_ChapterVerse_OverridesLlmType()
    {
        // LLM returns "theme" but local rule must override to "biblical_reference"
        var groqMock = new Mock<IGroqService>();
        groqMock.Setup(g => g.CompleteAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("""{"type":"theme","tags":{}}""");

        var loggerMock = new Mock<ILogger<SearchIndexClassifierService>>();
        var svc = new SearchIndexClassifierService(groqMock.Object, loggerMock.Object);

        var result = await svc.ClassifyAsync("João 3:16");
        Assert.Equal("biblical_reference", result.Type);
    }

    [Fact]
    public async Task ClassifyAsync_GeneralTerm_ParsesLlmTags()
    {
        var groqMock = new Mock<IGroqService>();
        groqMock.Setup(g => g.CompleteAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("""
            {
              "type": "mood",
              "tags": {
                "moods": ["alegria", "paz"],
                "themes": ["louvor"]
              }
            }
            """);

        var loggerMock = new Mock<ILogger<SearchIndexClassifierService>>();
        var svc = new SearchIndexClassifierService(groqMock.Object, loggerMock.Object);

        var result = await svc.ClassifyAsync("alegria");
        Assert.Equal("mood", result.Type);
        Assert.Contains("alegria", result.Tags.Moods);
        Assert.Contains("paz",     result.Tags.Moods);
        Assert.Contains("louvor",  result.Tags.Themes);
    }

    [Fact]
    public async Task ClassifyAsync_LlmFails_FallsBackToGeneral()
    {
        var groqMock = new Mock<IGroqService>();
        groqMock.Setup(g => g.CompleteAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var loggerMock = new Mock<ILogger<SearchIndexClassifierService>>();
        var svc = new SearchIndexClassifierService(groqMock.Object, loggerMock.Object);

        var result = await svc.ClassifyAsync("louvor");
        Assert.Equal("general", result.Type);
        Assert.Equal("louvor", result.Input);
    }

    [Fact]
    public async Task ClassifyAsync_BookChapterInput_SeedsBiblicalReferenceTag()
    {
        // When LLM doesn't populate BiblicalReferences, classifier seeds it from the raw input
        var groqMock = new Mock<IGroqService>();
        groqMock.Setup(g => g.CompleteAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("""{"type":"theme","tags":{"themes":["salmos"]}}""");

        var loggerMock = new Mock<ILogger<SearchIndexClassifierService>>();
        var svc = new SearchIndexClassifierService(groqMock.Object, loggerMock.Object);

        var result = await svc.ClassifyAsync("Salmo 91");
        Assert.Equal("biblical_reference", result.Type);
        Assert.NotEmpty(result.Tags.BiblicalReferences);
        Assert.Equal("Salmo 91", result.Tags.BiblicalReferences[0]);
    }

    [Fact]
    public async Task ClassifyAsync_WorshipMomentType_PopulatesCorrectly()
    {
        var groqMock = new Mock<IGroqService>();
        groqMock.Setup(g => g.CompleteAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("""
            {
              "type": "worship_moment",
              "tags": {
                "worship_moments": ["ofertório", "coleta"],
                "contexts": ["culto dominical"]
              }
            }
            """);

        var loggerMock = new Mock<ILogger<SearchIndexClassifierService>>();
        var svc = new SearchIndexClassifierService(groqMock.Object, loggerMock.Object);

        var result = await svc.ClassifyAsync("momento de ofertório");
        Assert.Equal("worship_moment", result.Type);
        Assert.Contains("ofertório", result.Tags.WorshipMoments);
        Assert.Contains("culto dominical", result.Tags.Contexts);
    }
}
