namespace PersonalAssistant.Core.Tests.WhatsApp;

internal static class SamplePayloads
{
    public const string TextMessage = """
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "100000000000000",
            "changes": [{
              "field": "messages",
              "value": {
                "messaging_product": "whatsapp",
                "metadata": { "display_phone_number": "15550000000", "phone_number_id": "200000000000000" },
                "contacts": [{ "profile": { "name": "Test User" }, "wa_id": "353800000001" }],
                "messages": [{
                  "from": "353800000001",
                  "id": "wamid.TEST1",
                  "timestamp": "1762200000",
                  "type": "text",
                  "text": { "body": "Oi, tudo bem?" }
                }]
              }
            }]
          }]
        }
        """;

    public const string TwoTextMessages = """
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "100000000000000",
            "changes": [{
              "field": "messages",
              "value": {
                "messaging_product": "whatsapp",
                "messages": [
                  { "from": "353800000001", "id": "wamid.TEST1", "timestamp": "1762200000", "type": "text", "text": { "body": "Primeira" } },
                  { "from": "353800000002", "id": "wamid.TEST2", "timestamp": "1762200005", "type": "text", "text": { "body": "Segunda" } }
                ]
              }
            }]
          }]
        }
        """;

    public const string StatusOnly = """
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

    public const string ImageMessage = """
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "100000000000000",
            "changes": [{
              "field": "messages",
              "value": {
                "messaging_product": "whatsapp",
                "messages": [{
                  "from": "353800000001",
                  "id": "wamid.TEST3",
                  "timestamp": "1762200000",
                  "type": "image",
                  "image": { "id": "300000000000000", "mime_type": "image/jpeg" }
                }]
              }
            }]
          }]
        }
        """;
}
