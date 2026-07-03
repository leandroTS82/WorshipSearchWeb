using Microsoft.Extensions.Logging;
using Moq;
using WorshipSearchWeb.Models;
using WorshipSearchWeb.Prompts;
using WorshipSearchWeb.Services;
using Xunit;

namespace WorshipSearchWeb.Tests.Tests;

public class MusicEnrichmentServiceTests
{
    // ── StripMarkdown ─────────────────────────────────────────────────────────

    [Fact]
    public void StripMarkdown_RemovesCodeFences()
    {
        var raw = "```json\n{\"themes\":[\"louvor\"]}\n```";
        var clean = MusicEnrichmentService.StripMarkdown(raw);
        Assert.Equal("{\"themes\":[\"louvor\"]}", clean);
    }

    [Fact]
    public void StripMarkdown_RemovesAnonymousCodeFence()
    {
        var raw = "```\n{\"themes\":[]}\n```";
        var clean = MusicEnrichmentService.StripMarkdown(raw);
        Assert.Equal("{\"themes\":[]}", clean);
    }

    [Fact]
    public void StripMarkdown_NoFences_ReturnsInputTrimmed()
    {
        var raw = "  {\"themes\":[\"fe\"]}  ";
        Assert.Equal("{\"themes\":[\"fe\"]}", MusicEnrichmentService.StripMarkdown(raw));
    }

    // ── ParseTagsOnly ─────────────────────────────────────────────────────────

    [Fact]
    public void ParseTagsOnly_FullJson_ParsesAllFields()
    {
        var json = """
        {
          "themes": ["louvor", "adoração"],
          "moods": ["alegria"],
          "contexts": ["culto"],
          "keywords": ["glória"],
          "biblical_topics": ["graça"],
          "biblical_references": ["João 3:16"],
          "biblical_books": ["João"],
          "biblical_characters": ["Jesus"],
          "synonyms": ["glória"],
          "worship_style": "contemporâneo",
          "energy_level": "alto",
          "occasion": "domingo",
          "worship_moments": ["ofertório"]
        }
        """;

        var meta = MusicEnrichmentService.ParseTagsOnly(json);
        Assert.Equal(new[] { "louvor", "adoração" }, meta.Themes);
        Assert.Equal(new[] { "alegria" },            meta.Moods);
        Assert.Equal(new[] { "culto" },              meta.Contexts);
        Assert.Equal(new[] { "glória" },             meta.Keywords);
        Assert.Equal(new[] { "graça" },              meta.BiblicalTopics);
        Assert.Equal(new[] { "João 3:16" },          meta.BiblicalReferences);
        Assert.Equal(new[] { "João" },               meta.BiblicalBooks);
        Assert.Equal(new[] { "Jesus" },              meta.BiblicalCharacters);
        Assert.Equal(new[] { "glória" },             meta.Synonyms);
        Assert.Equal("contemporâneo",                meta.WorshipStyle);
        Assert.Equal("alto",                         meta.EnergyLevel);
        Assert.Equal("domingo",                      meta.Occasion);
        Assert.Equal(new[] { "ofertório" },          meta.WorshipMoments);
    }

    [Fact]
    public void ParseTagsOnly_PartialJson_LeavesAbsentFieldsEmpty()
    {
        var json = """{"themes":["louvor"]}""";
        var meta = MusicEnrichmentService.ParseTagsOnly(json);
        Assert.Equal(new[] { "louvor" }, meta.Themes);
        Assert.Empty(meta.Moods);
        Assert.Empty(meta.BiblicalReferences);
        Assert.Equal(string.Empty, meta.WorshipStyle);
    }

    [Fact]
    public void ParseTagsOnly_InvalidJson_ReturnsEmptyMetadata()
    {
        var meta = MusicEnrichmentService.ParseTagsOnly("not valid json {{{{");
        Assert.Empty(meta.Themes);
        Assert.Empty(meta.Moods);
    }

    [Fact]
    public void ParseTagsOnly_EmptyString_ReturnsEmptyMetadata()
    {
        var meta = MusicEnrichmentService.ParseTagsOnly(string.Empty);
        Assert.Empty(meta.Themes);
    }

    [Fact]
    public void ParseTagsOnly_WithMarkdownFences_StillParsesCorrectly()
    {
        var json = "```json\n{\"themes\":[\"paz\"]}\n```";
        var meta = MusicEnrichmentService.ParseTagsOnly(json);
        Assert.Equal(new[] { "paz" }, meta.Themes);
    }

    // ── EnrichParagraphAsync ──────────────────────────────────────────────────

    private static (Mock<IGroqService>, Mock<IPromptBuilder>, Mock<IAppLogger>, MusicEnrichmentService)
        BuildService(string groqResponse)
    {
        var groqMock    = new Mock<IGroqService>();
        var builderMock = new Mock<IPromptBuilder>();
        var loggerMock  = new Mock<ILogger<MusicEnrichmentService>>();
        var appLogger   = new Mock<IAppLogger>();

        groqMock
            .Setup(g => g.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync((groqResponse, "test-key"));

        builderMock
            .Setup(b => b.BuildParagraphPrompt(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<SearchMetadata>()))
            .Returns("mocked_paragraph_prompt");
        builderMock
            .Setup(b => b.BuildSummaryPrompt(It.IsAny<MusicDocument>()))
            .Returns("mocked_summary_prompt");

        appLogger.Setup(a => a.InfoAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);
        appLogger.Setup(a => a.WarnAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);
        appLogger.Setup(a => a.ErrorAsync(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        var svc = new MusicEnrichmentService(
            groqMock.Object, builderMock.Object, loggerMock.Object, appLogger.Object);

        return (groqMock, builderMock, appLogger, svc);
    }

    [Fact]
    public async Task EnrichParagraphAsync_ValidJson_ReturnsPopulatedMetadata()
    {
        var response = """{"themes":["louvor","adoração"],"moods":["alegria"],"keywords":["glória"]}""";
        var (_, _, _, svc) = BuildService(response);

        var (tags, _) = await svc.EnrichParagraphAsync("letra", "Título", "Artista", new SearchMetadata());

        Assert.Contains("louvor",   tags.Themes);
        Assert.Contains("adoração", tags.Themes);
        Assert.Contains("alegria",  tags.Moods);
        Assert.Contains("glória",   tags.Keywords);
    }

    [Fact]
    public async Task EnrichParagraphAsync_LogsSuccess()
    {
        var response = """{"themes":["louvor"]}""";
        var (_, _, appLogger, svc) = BuildService(response);

        await svc.EnrichParagraphAsync("letra", "T", "A", new SearchMetadata());

        appLogger.Verify(a => a.InfoAsync("enrich_paragraph", It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task EnrichParagraphAsync_GroqThrows_LogsErrorAndRethrows()
    {
        var groqMock    = new Mock<IGroqService>();
        var builderMock = new Mock<IPromptBuilder>();
        var loggerMock  = new Mock<ILogger<MusicEnrichmentService>>();
        var appLogger   = new Mock<IAppLogger>();

        groqMock
            .Setup(g => g.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("Groq down"));
        builderMock
            .Setup(b => b.BuildParagraphPrompt(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<SearchMetadata>()))
            .Returns("prompt");
        appLogger.Setup(a => a.ErrorAsync(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        var svc = new MusicEnrichmentService(
            groqMock.Object, builderMock.Object, loggerMock.Object, appLogger.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.EnrichParagraphAsync("letra", "T", "A", new SearchMetadata()));

        appLogger.Verify(a => a.ErrorAsync("enrich_paragraph_fail", It.IsAny<Exception>(), It.IsAny<object>()), Times.Once);
    }

    // ── EnrichSummaryAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task EnrichSummaryAsync_ValidJson_ReturnsParsed7Tuple()
    {
        var response = """
        {
          "summary": "Música de louvor",
          "explanation": "Explica o louvor",
          "practical_application": "Usar no culto",
          "worship_style": "contemporâneo",
          "energy_level": "alto",
          "occasion": "domingo",
          "worship_moments": ["ofertório", "louvor"]
        }
        """;

        var (_, _, _, svc) = BuildService(response);
        var doc = new MusicDocument { Title = "T", Artist = "A", Lyrics = "L" };

        var (summary, explanation, practical, style, energy, occasion, moments, _) =
            await svc.EnrichSummaryAsync(doc);

        Assert.Equal("Música de louvor",    summary);
        Assert.Equal("Explica o louvor",    explanation);
        Assert.Equal("Usar no culto",       practical);
        Assert.Equal("contemporâneo",       style);
        Assert.Equal("alto",                energy);
        Assert.Equal("domingo",             occasion);
        Assert.Equal(new[] { "ofertório", "louvor" }, moments);
    }

    [Fact]
    public async Task EnrichSummaryAsync_ResponseWithProsePreamble_ExtractsJson()
    {
        var response = """
        Aqui está a análise da música:
        {
          "summary": "Hino de graça",
          "explanation": "",
          "practical_application": "",
          "worship_style": "",
          "energy_level": "",
          "occasion": "",
          "worship_moments": []
        }
        Espero que ajude.
        """;

        var (_, _, _, svc) = BuildService(response);
        var doc = new MusicDocument { Title = "T", Artist = "A" };

        var (summary, _, _, _, _, _, _, _) = await svc.EnrichSummaryAsync(doc);
        Assert.Equal("Hino de graça", summary);
    }

    [Fact]
    public async Task EnrichSummaryAsync_NoJson_ReturnsEmptyTuple()
    {
        var (_, _, appLogger, svc) = BuildService("Não consegui gerar um JSON válido.");
        var doc = new MusicDocument { Title = "T", Artist = "A" };

        var (summary, explanation, practical, style, energy, occasion, moments, _) =
            await svc.EnrichSummaryAsync(doc);

        Assert.Equal(string.Empty, summary);
        Assert.Equal(string.Empty, explanation);
        Assert.Empty(moments);
        appLogger.Verify(a => a.WarnAsync("enrich_summary_no_json", It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task EnrichSummaryAsync_LogsSuccess()
    {
        var response = """
        {"summary":"S","explanation":"E","practical_application":"P",
         "worship_style":"W","energy_level":"L","occasion":"O","worship_moments":[]}
        """;
        var (_, _, appLogger, svc) = BuildService(response);
        var doc = new MusicDocument { Title = "T", Artist = "A" };

        await svc.EnrichSummaryAsync(doc);
        appLogger.Verify(a => a.InfoAsync("enrich_summary", It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task EnrichSummaryAsync_GroqThrows_LogsErrorAndRethrows()
    {
        var groqMock    = new Mock<IGroqService>();
        var builderMock = new Mock<IPromptBuilder>();
        var loggerMock  = new Mock<ILogger<MusicEnrichmentService>>();
        var appLogger   = new Mock<IAppLogger>();

        groqMock
            .Setup(g => g.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ThrowsAsync(new HttpRequestException("timeout"));
        builderMock
            .Setup(b => b.BuildSummaryPrompt(It.IsAny<MusicDocument>()))
            .Returns("prompt");
        appLogger.Setup(a => a.ErrorAsync(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        var svc = new MusicEnrichmentService(
            groqMock.Object, builderMock.Object, loggerMock.Object, appLogger.Object);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            svc.EnrichSummaryAsync(new MusicDocument { Title = "T", Artist = "A" }));

        appLogger.Verify(a => a.ErrorAsync("enrich_summary_fail", It.IsAny<Exception>(), It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task EnrichSummaryAsync_WithMarkdownFences_StillParsesJson()
    {
        var response = """
        ```json
        {
          "summary": "Belo hino",
          "explanation": "",
          "practical_application": "",
          "worship_style": "clássico",
          "energy_level": "baixo",
          "occasion": "",
          "worship_moments": ["meditação"]
        }
        ```
        """;

        var (_, _, _, svc) = BuildService(response);
        var doc = new MusicDocument { Title = "T", Artist = "A" };

        var (summary, _, _, style, _, _, moments, _) = await svc.EnrichSummaryAsync(doc);
        Assert.Equal("Belo hino", summary);
        Assert.Equal("clássico",  style);
        Assert.Contains("meditação", moments);
    }
}
