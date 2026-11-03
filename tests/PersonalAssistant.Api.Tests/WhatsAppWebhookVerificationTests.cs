using System.Net;

namespace PersonalAssistant.Api.Tests;

public class WhatsAppWebhookVerificationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Challenge = "1158201444";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_ReturnsChallenge_WhenVerifyTokenIsValid()
    {
        var response = await _client.GetAsync(
            $"/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token={ApiFactory.VerifyToken}&hub.challenge={Challenge}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Challenge, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Get_ReturnsForbidden_WhenVerifyTokenIsInvalid()
    {
        var response = await _client.GetAsync(
            $"/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=wrong-token&hub.challenge={Challenge}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
