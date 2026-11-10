using System.Text.Json;

namespace PersonalAssistant.Core.Assistant;

/// <summary>
/// An action the assistant can take during a conversation, such as saving an appointment.
/// The assistant decides when to use it; the app runs it and sends the result back.
/// </summary>
public interface IAssistantTool
{
    /// <summary>A short name in snake_case, for example "add_appointment".</summary>
    string Name { get; }

    /// <summary>Explains to the assistant what the tool does and when to use it.</summary>
    string Description { get; }

    /// <summary>The JSON Schema of the input, as a JSON object.</summary>
    string InputSchema { get; }

    /// <summary>
    /// Runs the tool and returns a short result for the assistant. Throw <see cref="AssistantToolException"/>
    /// for problems the assistant should explain to the user, such as a missing title.
    /// </summary>
    Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default);
}
