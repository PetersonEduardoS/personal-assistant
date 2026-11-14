namespace PersonalAssistant.Core.Notes;

internal static class NoteText
{
    public const string ContextHeader = "## Notas salvas pelo chat";

    /// <summary>The line format shared by the context and list_notes, for example "(nota 3) Gosta de café".</summary>
    public static string Describe(UserNote note) => $"(nota {note.Id}) {note.Content}";
}
