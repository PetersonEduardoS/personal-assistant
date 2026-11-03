namespace PersonalAssistant.Infrastructure.Claude;

public sealed class ClaudeOptions
{
    public const string SectionName = "Anthropic";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.anthropic.com/";

    public string ApiVersion { get; set; } = "2023-06-01";

    public string Model { get; set; } = "claude-haiku-5-5";

    public int MaxTokens { get; set; } = 1024;

    public string SystemPrompt { get; set; } =
        "Você é o assistente pessoal do Eduardo e da Karina e responde pelo WhatsApp. " +
        "Responda sempre em português do Brasil, de forma curta, clara e amigável. " +
        "Não use tabelas nem títulos em Markdown, porque o WhatsApp não mostra. " +
        "Para destacar algo, use *asteriscos simples*.";
}
