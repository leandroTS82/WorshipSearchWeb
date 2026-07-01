using WorshipSearchWeb.Models;
using WorshipSearchWeb.Prompts;
using Xunit;

namespace WorshipSearchWeb.Tests.Tests;

public class PromptBuilderTests
{
    private readonly PromptBuilder _builder = new();

    // ── BuildParagraphPrompt ─────────────────────────────────────────────────

    [Fact]
    public void BuildParagraphPrompt_ContainsTitleAndArtist()
    {
        var prompt = _builder.BuildParagraphPrompt(
            "Glória a Deus nas alturas",
            "Hillsong",
            "Praise",
            new SearchMetadata());

        Assert.Contains("Hillsong", prompt);
        Assert.Contains("Praise",   prompt);
        Assert.Contains("Glória a Deus nas alturas", prompt);
    }

    [Fact]
    public void BuildParagraphPrompt_NoUnresolvedPlaceholders()
    {
        var existing = new SearchMetadata
        {
            Themes             = new() { "louvor" },
            Moods              = new() { "alegria" },
            Contexts           = new() { "culto" },
            Keywords           = new() { "adoração" },
            BiblicalTopics     = new() { "graça" },
            BiblicalReferences = new() { "João 3:16" },
            BiblicalBooks      = new() { "João" },
            BiblicalCharacters = new() { "Jesus" },
            Synonyms           = new() { "glória" },
            WorshipStyle       = "contemporâneo",
            EnergyLevel        = "alto",
            Occasion           = "domingo",
            WorshipMoments     = new() { "ofertório" }
        };

        var prompt = _builder.BuildParagraphPrompt("letra da música", "Artista", "Título", existing);
        Assert.DoesNotContain("{existing_themes}",             prompt);
        Assert.DoesNotContain("{existing_moods}",              prompt);
        Assert.DoesNotContain("{existing_contexts}",           prompt);
        Assert.DoesNotContain("{existing_keywords}",           prompt);
        Assert.DoesNotContain("{existing_biblical_topics}",    prompt);
        Assert.DoesNotContain("{existing_biblical_references}",prompt);
        Assert.DoesNotContain("{existing_biblical_books}",     prompt);
        Assert.DoesNotContain("{existing_biblical_characters}",prompt);
        Assert.DoesNotContain("{existing_synonyms}",           prompt);
        Assert.DoesNotContain("{existing_worship_style}",      prompt);
        Assert.DoesNotContain("{existing_energy_level}",       prompt);
        Assert.DoesNotContain("{existing_occasion}",           prompt);
        Assert.DoesNotContain("{existing_worship_moments}",    prompt);
        Assert.DoesNotContain("{title}",                       prompt);
        Assert.DoesNotContain("{artist}",                      prompt);
        Assert.DoesNotContain("{paragraph}",                   prompt);
    }

    [Fact]
    public void BuildParagraphPrompt_EmptyExisting_ShowsNonePlaceholder()
    {
        var prompt = _builder.BuildParagraphPrompt("letra", "Artista", "Título", new SearchMetadata());
        // When list is empty, Join returns "(none)"
        Assert.Contains("(none)", prompt);
    }

    [Fact]
    public void BuildParagraphPrompt_ExistingTagsAppearedInPrompt()
    {
        var existing = new SearchMetadata { Themes = new() { "redenção", "graça" } };
        var prompt = _builder.BuildParagraphPrompt("letra", "A", "T", existing);
        Assert.Contains("redenção", prompt);
        Assert.Contains("graça",    prompt);
    }

    // ── BuildSummaryPrompt ───────────────────────────────────────────────────

    [Fact]
    public void BuildSummaryPrompt_ContainsTitleArtistAndLyrics()
    {
        var doc = new MusicDocument
        {
            Title  = "Sola Gratia",
            Artist = "Torre de Marfim",
            Lyrics = "Somente pela graça sou salvo"
        };

        var prompt = _builder.BuildSummaryPrompt(doc);
        Assert.Contains("Sola Gratia",                  prompt);
        Assert.Contains("Torre de Marfim",              prompt);
        Assert.Contains("Somente pela graça sou salvo", prompt);
    }

    [Fact]
    public void BuildSummaryPrompt_NoUnresolvedPlaceholders()
    {
        var doc = new MusicDocument
        {
            Title  = "Título",
            Artist = "Artista",
            Lyrics = "Letra aqui",
            Search = new SearchMetadata { Themes = new() { "louvor" } }
        };

        var prompt = _builder.BuildSummaryPrompt(doc);
        Assert.DoesNotContain("{title}",     prompt);
        Assert.DoesNotContain("{artist}",    prompt);
        Assert.DoesNotContain("{tags_json}", prompt);
        Assert.DoesNotContain("{lyrics}",    prompt);
    }

    [Fact]
    public void BuildSummaryPrompt_TagsSerializedAsJson()
    {
        var doc = new MusicDocument
        {
            Title  = "T",
            Artist = "A",
            Lyrics = "L",
            Search = new SearchMetadata { Themes = new() { "paz", "esperança" } }
        };

        var prompt = _builder.BuildSummaryPrompt(doc);
        Assert.Contains("paz",      prompt);
        Assert.Contains("esperança",prompt);
    }
}
