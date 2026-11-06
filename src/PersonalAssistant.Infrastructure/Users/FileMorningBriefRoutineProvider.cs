using System.Text.Json;
using System.Text.Json.Serialization;
using PersonalAssistant.Core.MorningBriefs;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Infrastructure.Users;

/// <summary>Loads explicit private routine data. Missing or malformed configured files fail closed.</summary>
public sealed class FileMorningBriefRoutineProvider : IMorningBriefRoutineProvider
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<MorningBriefRoutine?> GetAsync(UserProfile user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(user.MorningBriefRoutineFilePath)) return null;
        await using var stream = File.OpenRead(user.MorningBriefRoutineFilePath);
        var routine = await JsonSerializer.DeserializeAsync<MorningBriefRoutine>(stream, Options, cancellationToken)
            ?? throw new InvalidOperationException("The morning brief routine file is empty.");
        routine.Validate();
        return routine;
    }
}
