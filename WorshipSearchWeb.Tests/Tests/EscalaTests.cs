using WorshipSearchWeb.Controllers;
using WorshipSearchWeb.Models;
using WorshipSearchWeb.Services;
using Xunit;

namespace WorshipSearchWeb.Tests.Tests;

public class EscalaBuildCopyTextTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static EscalaDocument Doc(string titulo = "Ceia da manhã", string data = "2026-07-06",
        string obs = "", string ministro = "", List<EscalaMusica>? musicas = null) =>
        new()
        {
            Titulo    = titulo,
            Data      = data,
            Observacao = obs,
            Ministro  = ministro,
            Musicas   = musicas ?? new()
        };

    private static EscalaMusica Song(string titulo, string artista,
        string url = "", string momento = "", string ob = "") =>
        new() { Titulo = titulo, Artista = artista, YoutubeUrl = url, Momento = momento, Obs = ob };

    // ── Sem observação — nenhuma saudação padrão ─────────────────────────────

    [Fact]
    public void BuildCopyText_NoObs_StartsWithDate()
    {
        var doc = Doc(data: "2026-07-05", obs: ""); // 2026-07-05 is Sunday
        var text = EscalaController.BuildCopyText(doc);
        Assert.StartsWith("Domingo", text);
    }

    [Fact]
    public void BuildCopyText_NoObs_DoesNotContainBoaDia()
    {
        var doc = Doc(obs: "");
        var text = EscalaController.BuildCopyText(doc);
        Assert.DoesNotContain("Bom dia", text);
    }

    // ── Com observação — aparece antes da data ─────────────────────────────────

    [Fact]
    public void BuildCopyText_WithObs_ObsIsFirstLine()
    {
        var doc = Doc(obs: "A paz pessoal!");
        var text = EscalaController.BuildCopyText(doc);
        Assert.StartsWith("A paz pessoal!", text);
    }

    [Fact]
    public void BuildCopyText_WithObs_DateAppearsAfter()
    {
        var doc = Doc(data: "2026-07-06", obs: "A paz!"); // 2026-07-06 = Segunda-feira
        var text = EscalaController.BuildCopyText(doc);
        Assert.Contains("Segunda-feira", text);
        Assert.True(text.IndexOf("Segunda-feira", StringComparison.Ordinal) >
                    text.IndexOf("A paz!", StringComparison.Ordinal));
    }

    // ── Data formatada corretamente ───────────────────────────────────────────

    [Theory]
    [InlineData("2026-07-05", "Domingo")]       // verified: 2026-07-05 = Sunday
    [InlineData("2026-07-06", "Segunda-feira")] // 2026-07-06 = Monday
    [InlineData("2026-07-07", "Terça-feira")]   // 2026-07-07 = Tuesday
    [InlineData("2026-07-11", "Sábado")]        // 2026-07-11 = Saturday
    public void BuildCopyText_DateLine_ContainsDayOfWeek(string iso, string dayName)
    {
        var doc = Doc(data: iso);
        var text = EscalaController.BuildCopyText(doc);
        Assert.Contains(dayName, text);
    }

    [Fact]
    public void BuildCopyText_DateLine_ContainsDdMm()
    {
        var doc = Doc(data: "2026-07-05"); // Sunday
        var text = EscalaController.BuildCopyText(doc);
        Assert.Contains("05/07", text);
    }

    [Fact]
    public void BuildCopyText_DateLine_HasNoAsterisks()
    {
        var doc = Doc();
        var text = EscalaController.BuildCopyText(doc);
        Assert.DoesNotContain("*", text);
    }

    // ── Título em minúsculas na linha de data ─────────────────────────────────

    [Fact]
    public void BuildCopyText_TituloInDateLine_IsLowercase()
    {
        var doc = Doc(titulo: "Ceia da Manhã");
        var text = EscalaController.BuildCopyText(doc);
        Assert.Contains("ceia da manhã", text);
        Assert.DoesNotContain("Ceia da Manhã", text);
    }

    // ── Músicas sem URL ────────────────────────────────────────────────────────

    [Fact]
    public void BuildCopyText_SongWithoutUrl_NoColonAfterTitle()
    {
        var doc = Doc(musicas: new() { Song("Os Guerreiros", "Renascer") });
        var text = EscalaController.BuildCopyText(doc);
        Assert.Contains("Os Guerreiros - Renascer", text);
        Assert.DoesNotContain("Os Guerreiros - Renascer:", text);
    }

    // ── Músicas com URL ────────────────────────────────────────────────────────

    [Fact]
    public void BuildCopyText_SongWithUrl_ColonAfterTitle()
    {
        var doc = Doc(musicas: new() { Song("Os Guerreiros", "Renascer", "https://youtu.be/abc") });
        var text = EscalaController.BuildCopyText(doc);
        Assert.Contains("Os Guerreiros - Renascer:", text);
        Assert.Contains("https://youtu.be/abc", text);
    }

    [Fact]
    public void BuildCopyText_SongWithUrl_UrlOnLineAfterTitle()
    {
        var doc = Doc(musicas: new() { Song("Os Guerreiros", "Renascer", "https://youtu.be/abc") });
        var lines = EscalaController.BuildCopyText(doc).Split('\n');
        var titleIdx = Array.FindIndex(lines, l => l.Contains("Os Guerreiros - Renascer:"));
        var urlIdx   = Array.FindIndex(lines, l => l.Contains("https://youtu.be/abc"));
        Assert.True(titleIdx >= 0);
        Assert.Equal(titleIdx + 1, urlIdx);
    }

    // ── Observação por música ─────────────────────────────────────────────────

    [Fact]
    public void BuildCopyText_SongObs_AppendsInParentheses()
    {
        var doc = Doc(musicas: new() { Song("Rude Cruz", "Fernandinho", ob: "Tom A") });
        var text = EscalaController.BuildCopyText(doc);
        Assert.Contains("Rude Cruz - Fernandinho (Tom A)", text);
    }

    // ── Seções de momento ─────────────────────────────────────────────────────

    [Fact]
    public void BuildCopyText_Momento_SectionHeaderAppears()
    {
        var doc = Doc(musicas: new()
        {
            Song("Getsêmani", "Leonardo Gonçalves", momento: "Ceia")
        });
        var text = EscalaController.BuildCopyText(doc);
        Assert.Contains("Ceia:", text);
    }

    [Fact]
    public void BuildCopyText_Momento_HeaderBeforeSong()
    {
        var doc = Doc(musicas: new()
        {
            Song("Getsêmani", "Leonardo Gonçalves", momento: "Ceia")
        });
        var text = EscalaController.BuildCopyText(doc);
        Assert.True(text.IndexOf("Ceia:", StringComparison.Ordinal) <
                    text.IndexOf("Getsêmani", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildCopyText_Momento_HeaderAppearsOnceForConsecutiveSongs()
    {
        var doc = Doc(musicas: new()
        {
            Song("Música 1", "Artista A", momento: "Ceia"),
            Song("Música 2", "Artista B", momento: "Ceia"),
        });
        var text = EscalaController.BuildCopyText(doc);
        Assert.Equal(1, CountOccurrences(text, "Ceia:"));
    }

    [Fact]
    public void BuildCopyText_MultipleMomentos_EachHeaderOnce()
    {
        var doc = Doc(musicas: new()
        {
            Song("M1", "A", momento: "Abertura"),
            Song("M2", "B", momento: "Ceia"),
        });
        var text = EscalaController.BuildCopyText(doc);
        Assert.Contains("Abertura:", text);
        Assert.Contains("Ceia:", text);
        Assert.Equal(1, CountOccurrences(text, "Abertura:"));
        Assert.Equal(1, CountOccurrences(text, "Ceia:"));
    }

    // ── Sem músicas — texto não quebra ────────────────────────────────────────

    [Fact]
    public void BuildCopyText_NoSongs_ReturnsDateLine()
    {
        var doc = Doc();
        var text = EscalaController.BuildCopyText(doc);
        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    // ── Data inválida — não lança exceção ─────────────────────────────────────

    [Fact]
    public void BuildCopyText_InvalidDate_DoesNotThrow()
    {
        var doc = Doc(data: "nao-e-data");
        var ex = Record.Exception(() => EscalaController.BuildCopyText(doc));
        Assert.Null(ex);
    }

    [Fact]
    public void BuildCopyText_EmptyDate_DoesNotThrow()
    {
        var doc = Doc(data: "");
        var ex = Record.Exception(() => EscalaController.BuildCopyText(doc));
        Assert.Null(ex);
    }

    // ── helper ───────────────────────────────────────────────────────────────

    private static int CountOccurrences(string text, string token)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(token, idx, StringComparison.Ordinal)) >= 0)
        {
            count++; idx += token.Length;
        }
        return count;
    }
}

public class StripLetrasSuffixTests
{
    [Theory]
    [InlineData("Fernandinho - LETRAS.MUS.BR", "Fernandinho")]
    [InlineData("Fernandinho - letras.mus.br",  "Fernandinho")]
    [InlineData("Fernandinho - LETRAS.MUS.BR  ", "Fernandinho")]
    [InlineData("Get Worship - LETRAS.MUS.BR",  "Get Worship")]
    [InlineData("Fernandinho",                   "Fernandinho")]  // nada para remover
    [InlineData("",                              "")]
    [InlineData("Artista - Álbum",               "Artista - Álbum")]  // não é o sufixo
    public void StripLetrasSuffix_RemovesOrPreserves(string input, string expected)
    {
        Assert.Equal(expected, MusicHtmlParser.StripLetrasSuffix(input));
    }
}

public class EscalaDocumentDefaultsTests
{
    [Fact]
    public void EscalaDocument_DefaultRascunho_IsTrue()
    {
        var doc = new EscalaDocument();
        Assert.True(doc.Rascunho);
    }

    [Fact]
    public void EscalaDocument_DefaultMusicas_IsEmptyList()
    {
        var doc = new EscalaDocument();
        Assert.NotNull(doc.Musicas);
        Assert.Empty(doc.Musicas);
    }

    [Fact]
    public void EscalaMusica_DefaultFields_AreEmpty()
    {
        var m = new EscalaMusica();
        Assert.Equal(string.Empty, m.YoutubeUrl);
        Assert.Equal(string.Empty, m.Momento);
        Assert.Equal(string.Empty, m.Obs);
        Assert.Equal(string.Empty, m.FileName);
    }
}
