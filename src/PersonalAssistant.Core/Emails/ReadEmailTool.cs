using System.Text.Json;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Emails;

internal sealed class ReadEmailTool(IEmailInbox inbox, UserProfile user, TimeZoneInfo timeZone) : IAssistantTool
{
    public string Name => "read_email";

    public string Description =>
        "Abre um e-mail da pessoa pelo id que aparece em list_emails e mostra o texto inteiro. " +
        "Nunca chute o id. " + EmailText.SafetyRule;

    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "email_id": { "type": "string", "description": "O id mostrado por list_emails" }
          },
          "required": ["email_id"]
        }
        """;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        var emailId = input.ValueKind == JsonValueKind.Object
            && input.TryGetProperty("email_id", out var value)
            && value.ValueKind is JsonValueKind.String or JsonValueKind.Number
                ? value.ToString().Trim()
                : string.Empty;

        if (emailId.Length == 0)
        {
            throw new AssistantToolException("Falta o campo email_id. Use list_emails para ver os ids.");
        }

        EmailMessage? email;

        try
        {
            email = await inbox.ReadAsync(user, emailId, cancellationToken);
        }
        catch (EmailUnavailableException)
        {
            return EmailText.Unavailable;
        }

        if (email is null)
        {
            throw new AssistantToolException($"Não existe e-mail com o id {emailId}. Use list_emails para ver os ids.");
        }

        var body = email.Body.Length > EmailLimits.MaxBodyLength
            ? email.Body[..EmailLimits.MaxBodyLength] + "\n(texto cortado)"
            : email.Body;

        return $"E-mail [id {email.Id}] ({EmailText.ExternalContentWarning})\n" +
            $"De: {email.From}\n" +
            $"Assunto: {email.Subject}\n" +
            $"Recebido: {EmailText.FormatTime(email.ReceivedAt, timeZone)}\n\n" +
            body;
    }
}
