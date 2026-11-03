using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace PersonalAssistant.Api.Tests;

internal static class WebhookRequests
{
    private const string Route = "/webhooks/whatsapp";

    public const string StatusPayload = """
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "100000000000000",
            "changes": [{
              "field": "messages",
              "value": {
                "messaging_product": "whatsapp",
                "statuses": [{ "id": "wamid.TEST1", "status": "delivered", "timestamp": "1762200010", "recipient_id": "353800000001" }]
              }
            }]
          }]
        }
        """;

    public static string TextMessagePayload(string from, string messageId, string text) => $$"""
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "100000000000000",
            "changes": [{
              "field": "messages",
              "value": {
                "messaging_product": "whatsapp",
                "messages": [{
                  "from": "{{from}}",
                  "id": "{{messageId}}",
                  "timestamp": "1762200000",
                  "type": "text",
                  "text": { "body": "{{text}}" }
                }]
              }
            }]
          }]
        }
        """;

    public static HttpRequestMessage CreateSignedPost(string payload, string appSecret)
    {
        var body = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body);

        return CreatePost(body, $"sha256={Convert.ToHexStringLower(hash)}");
    }

    public static HttpRequestMessage CreatePost(string payload, string signature) =>
        CreatePost(Encoding.UTF8.GetBytes(payload), signature);

    private static HttpRequestMessage CreatePost(byte[] body, string signature)
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = content };
        request.Headers.Add("X-Hub-Signature-256", signature);

        return request;
    }
}
