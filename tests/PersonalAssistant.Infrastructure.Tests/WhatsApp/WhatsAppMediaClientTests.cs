using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalAssistant.Core.Media;
using PersonalAssistant.Core.WhatsApp;
using PersonalAssistant.Infrastructure.WhatsApp;

namespace PersonalAssistant.Infrastructure.Tests.WhatsApp;

public class WhatsAppMediaClientTests
{
    private const string LookupUrl = "https://graph.facebook.com/v25.0/300";
    private const string FileUrl = "https://lookaside.fbsbx.com/whatsapp_business/attachments/?mid=300&hash=abc";

    private static readonly WhatsAppOptions Options = new()
    {
        AccessToken = "test-access-token",
        PhoneNumberId = "123456",
        GraphApiVersion = "v25.0"
    };

    private readonly RoutingHandler _handler = new();

    [Fact]
    public async Task DownloadAsync_LooksUpTheUrl_ThenDownloadsTheFile_WithTheToken()
    {
        _handler.Json(LookupUrl, $$"""{ "url": "{{FileUrl}}", "mime_type": "image/jpeg", "file_size": 4, "id": "300" }""");
        _handler.Bytes(FileUrl, [1, 2, 3, 4]);

        var media = await CreateClient().DownloadAsync("300", maxBytes: 100);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, media.Content);
        Assert.Equal("image/jpeg", media.MimeType);
        Assert.Equal(new[] { LookupUrl, FileUrl }, _handler.Requests.Select(request => request.Url).ToArray());
        Assert.All(_handler.Requests, request => Assert.Equal("Bearer test-access-token", request.Authorization));
    }

    [Fact]
    public async Task DownloadAsync_RefusesATooLargeFile_BeforeDownloadingIt()
    {
        _handler.Json(LookupUrl, $$"""{ "url": "{{FileUrl}}", "mime_type": "image/jpeg", "file_size": 6000000 }""");

        await Assert.ThrowsAsync<MediaTooLargeException>(() => CreateClient().DownloadAsync("300", maxBytes: 5_000_000));
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task DownloadAsync_StopsWhenTheFileIsLargerThanAnnounced()
    {
        _handler.Json(LookupUrl, $$"""{ "url": "{{FileUrl}}", "mime_type": "application/pdf" }""");
        _handler.Bytes(FileUrl, new byte[10]);

        await Assert.ThrowsAsync<MediaTooLargeException>(() => CreateClient().DownloadAsync("300", maxBytes: 5));
    }

    [Fact]
    public async Task DownloadAsync_ThrowsUnavailable_WhenMetaRejectsTheLookup()
    {
        _handler.Json(LookupUrl, """{ "error": { "message": "Unsupported get request" } }""", HttpStatusCode.NotFound);

        await Assert.ThrowsAsync<MediaUnavailableException>(() => CreateClient().DownloadAsync("300", maxBytes: 100));
    }

    [Theory]
    [InlineData("""{ "mime_type": "image/jpeg" }""")]
    [InlineData("""{ "url": "http://lookaside.fbsbx.com/file", "mime_type": "image/jpeg" }""")]
    [InlineData("not json")]
    public async Task DownloadAsync_ThrowsUnavailable_WhenTheLookupHasNoSafeUrl(string lookup)
    {
        _handler.Json(LookupUrl, lookup);

        await Assert.ThrowsAsync<MediaUnavailableException>(() => CreateClient().DownloadAsync("300", maxBytes: 100));
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task DownloadAsync_ThrowsUnavailable_WhenTheFileCannotBeDownloaded()
    {
        _handler.Json(LookupUrl, $$"""{ "url": "{{FileUrl}}", "mime_type": "image/jpeg" }""");
        _handler.Json(FileUrl, "expired", HttpStatusCode.NotFound);

        await Assert.ThrowsAsync<MediaUnavailableException>(() => CreateClient().DownloadAsync("300", maxBytes: 100));
    }

    private WhatsAppMediaClient CreateClient() =>
        new(new HttpClient(_handler), Options, NullLogger<WhatsAppMediaClient>.Instance);

    /// <summary>Answers each URL with a fixed response and records the requests.</summary>
    private sealed class RoutingHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = [];

        public List<(string Url, string? Authorization)> Requests { get; } = [];

        public void Json(string url, string body, HttpStatusCode status = HttpStatusCode.OK) =>
            _routes[url] = () => new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

        public void Bytes(string url, byte[] content) =>
            _routes[url] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            Requests.Add((url, request.Headers.Authorization?.ToString()));

            return Task.FromResult(_routes.TryGetValue(url, out var respond)
                ? respond()
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
