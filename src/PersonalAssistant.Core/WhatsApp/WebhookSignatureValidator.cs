using System.Security.Cryptography;
using System.Text;

namespace PersonalAssistant.Core.WhatsApp;

/// <summary>
/// Checks the X-Hub-Signature-256 header that Meta adds to every webhook request.
/// </summary>
public sealed class WebhookSignatureValidator(string appSecret)
{
    private const string SignaturePrefix = "sha256=";

    public bool IsValid(byte[] payload, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(appSecret) || string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        if (!signatureHeader.StartsWith(SignaturePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        byte[] receivedHash;

        try
        {
            receivedHash = Convert.FromHexString(signatureHeader[SignaturePrefix.Length..]);
        }
        catch (FormatException)
        {
            return false;
        }

        var expectedHash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), payload);

        return CryptographicOperations.FixedTimeEquals(receivedHash, expectedHash);
    }
}
