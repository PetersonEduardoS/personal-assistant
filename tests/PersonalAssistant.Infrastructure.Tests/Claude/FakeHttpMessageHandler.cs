using System.Net;
using System.Text;

namespace PersonalAssistant.Infrastructure.Tests.Claude;

/// <summary>
/// Replaces the network in tests: records every request and returns a fixed response.
/// </summary>
internal sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    public HttpMethod? LastMethod => Requests.LastOrDefault()?.Method;

    public Uri? LastRequestUri => Requests.LastOrDefault()?.Uri;

    public IReadOnlyDictionary<string, string> LastHeaders =>
        Requests.LastOrDefault()?.Headers ?? new Dictionary<string, string>();

    public string? LastRequestBody => Requests.LastOrDefault()?.Body;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add(new RecordedRequest(request.Method, request.RequestUri, headers, body));

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };
    }
}

internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri? Uri,
    IReadOnlyDictionary<string, string> Headers,
    string? Body);
