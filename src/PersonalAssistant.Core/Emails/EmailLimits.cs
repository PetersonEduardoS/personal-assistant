namespace PersonalAssistant.Core.Emails;

public static class EmailLimits
{
    public const int DefaultDays = 2;

    public const int MaxDays = 14;

    public const int MaxResults = 15;

    /// <summary>Longer bodies are cut, to keep each message to the assistant cheap.</summary>
    public const int MaxBodyLength = 6000;

    public const int SnippetLength = 200;
}
