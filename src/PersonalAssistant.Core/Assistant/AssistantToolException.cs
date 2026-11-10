namespace PersonalAssistant.Core.Assistant;

/// <summary>
/// A problem with a tool call that the assistant can explain or fix, for example a missing date.
/// The message is sent to the assistant, so it must not contain secrets.
/// </summary>
public sealed class AssistantToolException(string message) : Exception(message);
