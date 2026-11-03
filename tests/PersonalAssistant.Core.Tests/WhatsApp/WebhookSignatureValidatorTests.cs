using System.Security.Cryptography;
using System.Text;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Core.Tests.WhatsApp;

public class WebhookSignatureValidatorTests
{
    private const string AppSecret = "test-app-secret";

    private static readonly byte[] Payload =
        Encoding.UTF8.GetBytes("""{"object":"whatsapp_business_account"}""");

    private readonly WebhookSignatureValidator _validator = new(AppSecret);

    [Fact]
    public void IsValid_ReturnsTrue_WhenSignatureMatches()
    {
        var header = CreateSignatureHeader(Payload, AppSecret);

        Assert.True(_validator.IsValid(Payload, header));
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenSignedWithAnotherSecret()
    {
        var header = CreateSignatureHeader(Payload, "another-secret");

        Assert.False(_validator.IsValid(Payload, header));
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenPayloadWasChanged()
    {
        var header = CreateSignatureHeader(Payload, AppSecret);
        var changedPayload = Encoding.UTF8.GetBytes("""{"object":"changed"}""");

        Assert.False(_validator.IsValid(changedPayload, header));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha256=")]
    [InlineData("not-a-signature")]
    [InlineData("sha256=not-hex")]
    public void IsValid_ReturnsFalse_WhenHeaderIsMissingOrMalformed(string? header)
    {
        Assert.False(_validator.IsValid(Payload, header));
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenAppSecretIsNotConfigured()
    {
        var validator = new WebhookSignatureValidator(string.Empty);
        var header = CreateSignatureHeader(Payload, string.Empty);

        Assert.False(validator.IsValid(Payload, header));
    }

    private static string CreateSignatureHeader(byte[] payload, string secret)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload);

        return $"sha256={Convert.ToHexStringLower(hash)}";
    }
}
