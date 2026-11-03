namespace PersonalAssistant.Core.WhatsApp;

public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    public string AccessToken { get; set; } = string.Empty;

    public string AppSecret { get; set; } = string.Empty;

    public string VerifyToken { get; set; } = string.Empty;

    public string PhoneNumberId { get; set; } = string.Empty;

    public string[] AllowedSenders { get; set; } = [];

    public string GraphApiBaseUrl { get; set; } = "https://graph.facebook.com/";

    public string GraphApiVersion { get; set; } = "v25.0";
}
