using System.Net;

namespace PersonalAssistant.Infrastructure.Claude;

/// <summary>
/// Raised when the Claude API returns an error or an empty reply.
/// The message never includes the API key.
/// </summary>
public sealed class ClaudeApiException(HttpStatusCode statusCode, string message, string? responseBody = null)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string? ResponseBody { get; } = responseBody;
}
