using System.Net;
using System.Text;

namespace PersonalAssistant.Infrastructure.Tests.Calendars;

/// <summary>
/// A fake feed that can start failing or succeeding in the middle of a test.
/// </summary>
internal sealed class SwitchableHttpMessageHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
{
    private HttpStatusCode _statusCode = statusCode;
    private string _body = body;

    public List<Uri?> RequestUris { get; } = [];

    public int RequestCount => RequestUris.Count;

    public void Fail()
    {
        _statusCode = HttpStatusCode.ServiceUnavailable;
        _body = "Service Unavailable";
    }

    public void Succeed(string body)
    {
        _statusCode = HttpStatusCode.OK;
        _body = body;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestUris.Add(request.RequestUri);

        return Task.FromResult(new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_body, Encoding.UTF8, "text/calendar")
        });
    }
}
