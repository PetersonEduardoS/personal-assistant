using System.Net;

namespace PersonalAssistant.Infrastructure.WhatsApp;

/// <summary>
/// Raised when the WhatsApp Cloud API rejects a message.
/// The message never includes the access token.
/// </summary>
public sealed class WhatsAppApiException(HttpStatusCode statusCode, string message, string? responseBody = null)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string? ResponseBody { get; } = responseBody;
}
