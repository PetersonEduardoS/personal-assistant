namespace PersonalAssistant.Core.Users;

/// <summary>
/// How to read the user's mailbox over IMAP. The app password gives full access to the account:
/// it is a secret, kept only in the hosting variables and never logged.
/// </summary>
public sealed class UserEmailSettings
{
    public string ImapHost { get; set; } = string.Empty;

    public int Port { get; set; } = 993;

    public string Username { get; set; } = string.Empty;

    public string AppPassword { get; set; } = string.Empty;

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(ImapHost)
        && !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrWhiteSpace(AppPassword);
}
