namespace PersonalAssistant.Core.Users;

/// <summary>
/// Gets background information about a user, sent to the assistant with every message.
/// </summary>
public interface IUserContextProvider
{
    Task<string?> GetContextAsync(UserProfile user, CancellationToken cancellationToken = default);
}
