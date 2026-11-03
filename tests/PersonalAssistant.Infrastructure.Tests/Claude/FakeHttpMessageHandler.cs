using System.Net;
using System.Text;

namespace PersonalAssistant.Infrastructure.Tests.Claude;

/// <summary>
/// Replaces the network in tests: records the request and returns a fixed response.
/// </summary>
internal sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
{
    public HttpMethod? LastMethod { get; private set; }

    public Uri? LastRequestUri { get; private set; }

    public Dictionary<string, string> LastHeaders { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastMethod = request.Method;
        LastRequestUri = request.RequestUri;

        foreach (var header in request.Headers)
        {
            LastHeaders[header.Key] = string.Join(",", header.Value);
        }

        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };
    }
}
