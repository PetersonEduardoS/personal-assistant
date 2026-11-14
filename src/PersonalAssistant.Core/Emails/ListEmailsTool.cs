using System.Text;
using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Emails;

internal sealed class ListEmailsTool(
    IEmailInbox inbox,
    UserProfile user,
    TimeProvider clock,
    TimeZoneInfo timeZone) : IAssistantTool
{
    public string Name => "list_emails";

    public string Description =>
        "Lista os e-mails recentes da caixa de entrada da pessoa (sem promoções), com id, data, remetente, " +
        "assunto e um trecho. Use para perguntas como \"chegou e-mail da CCT?\" ou \"o recrutador respondeu?\". " +
        "Para ler um e-mail inteiro, use read_email com o id. " + EmailText.SafetyRule;

    public string InputSchema => $$"""
        {
          "type": "object",
          "properties": {
            "days": { "type": "integer", "description": "Quantos dias para trás, de 1 a {{EmailLimits.MaxDays}}. Padrão {{EmailLimits.DefaultDays}}." },
            "from": { "type": "string", "description": "Parte do remetente, por exemplo cct.ie ou linkedin" },
            "text": { "type": "string", "description": "Palavra que aparece no assunto ou no texto" }
          }
        }
        """;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        var days = ReadDays(input);
        var query = new EmailQuery(
            clock.GetUtcNow().AddDays(-days),
            ReadOptional(input, "from"),
            ReadOptional(input, "text"),
            EmailLimits.MaxResults);

        IReadOnlyList<EmailSummary> emails;

        try
        {
            emails = await inbox.ListRecentAsync(user, query, cancellationToken);
        }
        catch (EmailUnavailableException)
        {
            return EmailText.Unavailable;
        }

        if (emails.Count == 0)
        {
            return $"Nenhum e-mail encontrado nos últimos {days} dias.";
        }

        var result = new StringBuilder($"E-mails dos últimos {days} dias ({EmailText.ExternalContentWarning}):");

        foreach (var email in emails)
        {
            var unread = email.IsUnread ? " | não lido" : string.Empty;
            result.Append(
                $"\n- [id {email.Id}] {EmailText.FormatTime(email.ReceivedAt, timeZone)} | De: {email.From} | " +
                $"Assunto: {email.Subject}{unread}");

            if (!string.IsNullOrWhiteSpace(email.Snippet))
            {
                result.Append($"\n  Trecho: {email.Snippet}");
            }
        }

        return result.ToString();
    }

    private static int ReadDays(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object
            || !input.TryGetProperty("days", out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return EmailLimits.DefaultDays;
        }

        if (value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var days)
            && days is >= 1 and <= EmailLimits.MaxDays)
        {
            return days;
        }

        throw new AssistantToolException($"O campo days deve ser um número de 1 a {EmailLimits.MaxDays}.");
    }

    private static string? ReadOptional(JsonElement input, string name) =>
        input.ValueKind == JsonValueKind.Object
        && input.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;
}
