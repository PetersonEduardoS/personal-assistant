using System.Net;
using System.Text;

namespace PersonalAssistant.Infrastructure.Tests.Claude;

/// <summary>
/// Replaces the network in tests and answers each request with the next response of a list.
/// The last response repeats when the list runs out.
/// </summary>
internal sealed class SequenceHttpMessageHandler(params string[] responses) : HttpMessageHandler
{
    private int _next;

    public List<string> RequestBodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestBodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));

        var body = responses[Math.Min(_next, responses.Length - 1)];
        _next++;

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }
}
