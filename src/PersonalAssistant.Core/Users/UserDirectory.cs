namespace PersonalAssistant.Core.Users;

/// <summary>
/// Finds a registered user by phone number. Unknown numbers get no answer.
/// Numbers are compared by their digits, so "+353 80..." and "35380..." match.
/// </summary>
public sealed class UserDirectory
{
    public const string SectionName = "Users";

    private readonly Dictionary<string, UserProfile> _usersByPhoneNumber = new(StringComparer.Ordinal);

    public UserDirectory(IEnumerable<UserProfile> users)
    {
        foreach (var user in users)
        {
            var phoneNumber = Normalize(user.PhoneNumber);

            if (phoneNumber.Length > 0)
            {
                _usersByPhoneNumber[phoneNumber] = user;
            }
        }
    }

    public UserProfile? FindByPhoneNumber(string? phoneNumber) =>
        string.IsNullOrEmpty(phoneNumber)
            ? null
            : _usersByPhoneNumber.GetValueOrDefault(Normalize(phoneNumber));

    public IReadOnlyCollection<UserProfile> All => _usersByPhoneNumber.Values;

    private static string Normalize(string? phoneNumber) =>
        new((phoneNumber ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
}
