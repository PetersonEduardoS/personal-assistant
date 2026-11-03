using System.Net;

namespace PersonalAssistant.Api.Tests;

public class WhatsAppWebhookReceiveTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public WhatsAppWebhookReceiveTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Queue.Clear();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Post_ReturnsUnauthorized_WhenSignatureIsInvalid()
    {
        var payload = WebhookRequests.TextMessagePayload(ApiFactory.AllowedSender, "wamid.TEST1", "Oi");

        var response = await _client.SendAsync(WebhookRequests.CreatePost(payload, "sha256=0000"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_factory.Queue.EnqueuedMessages);
    }

    [Fact]
    public async Task Post_EnqueuesMessage_WhenSignatureIsValidAndSenderIsAllowed()
    {
        var payload = WebhookRequests.TextMessagePayload(ApiFactory.AllowedSender, "wamid.TEST1", "Oi");

        var response = await _client.SendAsync(WebhookRequests.CreateSignedPost(payload, ApiFactory.AppSecret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = Assert.Single(_factory.Queue.EnqueuedMessages);
        Assert.Equal("wamid.TEST1", message.MessageId);
        Assert.Equal("Oi", message.Text);
    }

    [Fact]
    public async Task Post_DoesNotEnqueueMessage_WhenSenderIsNotAllowed()
    {
        var payload = WebhookRequests.TextMessagePayload("353899999999", "wamid.TEST2", "Oi");

        var response = await _client.SendAsync(WebhookRequests.CreateSignedPost(payload, ApiFactory.AppSecret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_factory.Queue.EnqueuedMessages);
    }

    [Fact]
    public async Task Post_ReturnsOk_WhenPayloadHasNoTextMessages()
    {
        var response = await _client.SendAsync(
            WebhookRequests.CreateSignedPost(WebhookRequests.StatusPayload, ApiFactory.AppSecret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_factory.Queue.EnqueuedMessages);
    }
}
