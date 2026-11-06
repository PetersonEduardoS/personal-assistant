using System.Net;

namespace PersonalAssistant.Api.Tests;

public class MorningBriefTestEndpointTests
{
    [Fact]
    public async Task Post_IsDisabledWithoutKey()
    {
        using var factory = new ApiFactory();
        var response = await factory.CreateClient().PostAsync("/admin/morning-briefs/test/0", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    public async Task Post_RejectsMissingOrIncorrectKey(string? key)
    {
        using var factory = new ApiFactory { MorningBriefTestKey = new string('a', 64) };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/morning-briefs/test/0");
        if (key is not null) request.Headers.Add("X-Morning-Brief-Key", key);
        var response = await factory.CreateClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_DoesNotAllowArbitraryRecipients()
    {
        var key = new string('a', 64);
        using var factory = new ApiFactory { MorningBriefTestKey = key };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/morning-briefs/test/99");
        request.Headers.Add("X-Morning-Brief-Key", key);
        var response = await factory.CreateClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
