using PersonalAssistant.Core.Emails;
using PersonalAssistant.Infrastructure.Emails;

namespace PersonalAssistant.Infrastructure.Tests.Emails;

public class GmailSearchTests
{
    private static readonly DateTimeOffset Since = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Build_LooksAtTheInbox_WithoutPromotions_SinceTheDate()
    {
        var search = GmailSearch.Build(new EmailQuery(Since, null, null, 15));

        Assert.Equal($"in:inbox -category:promotions after:{Since.ToUnixTimeSeconds()}", search);
    }

    [Fact]
    public void Build_AddsTheSenderAndTheText()
    {
        var search = GmailSearch.Build(new EmailQuery(Since, "cct.ie", "CA2 feedback", 15));

        Assert.EndsWith(" from:(cct.ie) \"CA2 feedback\"", search);
    }

    [Fact]
    public void Build_RemovesCharactersThatWouldBreakTheSearch()
    {
        var search = GmailSearch.Build(new EmailQuery(Since, "a\"b) (c", "x\\\"y", 15));

        Assert.EndsWith(" from:(ab c) \"xy\"", search);
    }

    [Fact]
    public void Build_SkipsFiltersThatAreEmptyAfterCleaning()
    {
        var search = GmailSearch.Build(new EmailQuery(Since, "\"()\"", "  ", 15));

        Assert.DoesNotContain("from:", search);
        Assert.EndsWith($"after:{Since.ToUnixTimeSeconds()}", search);
    }
}
