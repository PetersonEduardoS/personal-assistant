using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Infrastructure.Users;

/// <summary>
/// Reads the user's context from a Markdown file.
/// The file is read on every message, so edits apply without restarting the app.
/// Long files are cut to keep the cost of each message under control.
/// </summary>
public sealed class FileUserContextProvider : IUserContextProvider
{
    public const int MaxLength = 8000;

    public async Task<string?> GetContextAsync(UserProfile user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(user.ContextFilePath) || !File.Exists(user.ContextFilePath))
        {
            return null;
        }

        var content = (await File.ReadAllTextAsync(user.ContextFilePath, cancellationToken)).Trim();

        if (content.Length == 0)
        {
            return null;
        }

        return content.Length > MaxLength ? content[..MaxLength] : content;
    }
}
