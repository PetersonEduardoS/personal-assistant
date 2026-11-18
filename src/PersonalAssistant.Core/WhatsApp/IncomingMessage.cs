namespace PersonalAssistant.Core.WhatsApp;

public enum IncomingMessageKind { Text, Image, Document, Unsupported }

/// <summary>
/// A photo or file sent on WhatsApp. Only its id comes with the webhook; the file is downloaded separately.
/// </summary>
public sealed record MessageAttachment(string MediaId, string MimeType, string? FileName);

/// <summary>
/// A message received from a WhatsApp user. For photos and files, <see cref="Text"/> is the caption, possibly empty.
/// <see cref="UnsupportedType"/> is the WhatsApp type (audio, video...) of a message the assistant cannot read yet.
/// </summary>
public sealed record IncomingMessage(
    string MessageId,
    string From,
    string Text,
    DateTimeOffset Timestamp,
    IncomingMessageKind Kind = IncomingMessageKind.Text,
    MessageAttachment? Attachment = null,
    string? UnsupportedType = null);
