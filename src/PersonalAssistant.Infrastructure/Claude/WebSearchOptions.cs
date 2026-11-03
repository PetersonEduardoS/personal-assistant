namespace PersonalAssistant.Infrastructure.Claude;

/// <summary>
/// Settings for the Claude web search tool. Each search costs extra,
/// so the number of searches per message is limited.
/// </summary>
public sealed class WebSearchOptions
{
    public bool Enabled { get; set; }

    public int MaxUses { get; set; } = 3;

    public string? City { get; set; }

    public string? Region { get; set; }

    public string? Country { get; set; }

    public string? Timezone { get; set; }
}
