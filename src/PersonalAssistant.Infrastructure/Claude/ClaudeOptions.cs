namespace PersonalAssistant.Infrastructure.Claude;

public sealed class ClaudeOptions
{
    public const string SectionName = "Anthropic";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.anthropic.com/";

    public string ApiVersion { get; set; } = "2023-06-01";

    public string Model { get; set; } = "claude-haiku-5-5";

    // Web search and model reasoning share this budget with the final reply.
    public int MaxTokens { get; set; } = 4096;

    public string SystemPrompt { get; set; } =
        "Você é o assistente pessoal do Eduardo e da Karina e responde pelo WhatsApp. " +
        "Responda sempre em português do Brasil, de forma curta, clara e amigável. " +
        "Não use tabelas nem títulos em Markdown, porque o WhatsApp não mostra. " +
        "Para destacar algo, use *asteriscos simples*. " +
        "Você recebe as mensagens recentes desta conversa, inclusive de dias anteriores, " +
        "e pode receber um contexto sobre a pessoa, que ela mesma escreve e atualiza.";

    public WebSearchOptions WebSearch { get; set; } = new();
}
