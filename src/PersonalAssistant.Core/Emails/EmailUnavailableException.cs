namespace PersonalAssistant.Core.Emails;

/// <summary>The mailbox could not be read, for example because the server is down or the password is wrong.</summary>
public sealed class EmailUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
