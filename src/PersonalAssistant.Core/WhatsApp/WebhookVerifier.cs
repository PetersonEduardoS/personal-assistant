using System.Security.Cryptography;
using System.Text;

namespace PersonalAssistant.Core.WhatsApp;

/// <summary>
/// Validates the verification request Meta sends when a webhook URL is registered.
/// </summary>
public sealed class WebhookVerifier(string expectedVerifyToken)
{
    private const string SubscribeMode = "subscribe";

    /// <summary>
    /// Returns the challenge when the request is valid, otherwise null.
    /// </summary>
    public string? Verify(string? mode, string? verifyToken, string? challenge)
    {
        if (string.IsNullOrWhiteSpace(expectedVerifyToken))
        {
            return null;
        }

        if (mode != SubscribeMode || string.IsNullOrEmpty(verifyToken) || string.IsNullOrEmpty(challenge))
        {
            return null;
        }

        return TokensMatch(verifyToken, expectedVerifyToken) ? challenge : null;
    }

    private static bool TokensMatch(string received, string expected)
    {
        var receivedBytes = Encoding.UTF8.GetBytes(received);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        return CryptographicOperations.FixedTimeEquals(receivedBytes, expectedBytes);
    }
}
