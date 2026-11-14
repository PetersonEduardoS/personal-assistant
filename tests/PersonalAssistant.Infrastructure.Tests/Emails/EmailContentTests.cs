using PersonalAssistant.Infrastructure.Emails;

namespace PersonalAssistant.Infrastructure.Tests.Emails;

public class EmailContentTests
{
    [Fact]
    public void ToPlainText_PrefersTheTextBody()
    {
        Assert.Equal("Olá, Eduardo", EmailContent.ToPlainText("  Olá, Eduardo  ", "<p>HTML</p>"));
    }

    [Fact]
    public void ToPlainText_TurnsHtmlIntoReadableText()
    {
        var html = "<html><head><title>x</title></head><style>.a { color: red }</style>" +
            "<p>Olá&nbsp;Eduardo,</p><script>alert(1)</script><div>A entrevista é na <b>terça</b> às 10:00.<br>Abraços</div>";

        var text = EmailContent.ToPlainText(null, html);

        Assert.Equal("Olá Eduardo,\nA entrevista é na terça às 10:00.\nAbraços", text);
    }

    [Fact]
    public void ToPlainText_ReturnsEmpty_WhenThereIsNoBody()
    {
        Assert.Equal(string.Empty, EmailContent.ToPlainText(null, null));
    }

    [Fact]
    public void Snippet_CollapsesTheWhitespace()
    {
        Assert.Equal("Hi Eduardo, are you free?", EmailContent.Snippet("Hi   Eduardo,\r\n\r\n are you free?", 200));
    }

    [Fact]
    public void Snippet_CutsAtTheLimit()
    {
        var snippet = EmailContent.Snippet(new string('a', 300), 200);

        Assert.Equal(new string('a', 200) + "…", snippet);
    }

    [Theory]
    [InlineData("Ana Byrne", "ana@acme.ie", "Ana Byrne <ana@acme.ie>")]
    [InlineData("", "ana@acme.ie", "ana@acme.ie")]
    [InlineData(null, "ana@acme.ie", "ana@acme.ie")]
    [InlineData(null, null, "(remetente desconhecido)")]
    public void FormatSender_ShowsTheNameAndTheAddress(string? name, string? address, string expected)
    {
        Assert.Equal(expected, EmailContent.FormatSender(name, address));
    }
}
