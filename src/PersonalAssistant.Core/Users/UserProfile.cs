namespace PersonalAssistant.Core.Users;

/// <summary>
/// A person who can talk to the assistant.
/// </summary>
public sealed class UserProfile
{
    public string PhoneNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? ContextFilePath { get; set; }
}
