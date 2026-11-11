using System.Globalization;
using System.Text.Json;
using PersonalAssistant.Core.Assistant;

namespace PersonalAssistant.Core.Appointments;

/// <summary>
/// Shared formats and input reading for the appointment tools.
/// </summary>
internal static class AppointmentText
{
    public const string DateFormat = "yyyy-MM-dd";
    public const string TimeFormat = "HH:mm";

    /// <summary>
    /// The clean format the user sees, for example "Corte de cabelo dia 13/11 às 13:30".
    /// </summary>
    public static string Describe(Appointment appointment) =>
        $"{appointment.Title} dia {appointment.Date.ToString("dd/MM", CultureInfo.InvariantCulture)} " +
        $"às {appointment.Time.ToString(TimeFormat, CultureInfo.InvariantCulture)}";

    public static string? GetOptionalString(JsonElement input, string name) =>
        input.ValueKind == JsonValueKind.Object
        && input.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static string GetRequiredString(JsonElement input, string name) =>
        GetOptionalString(input, name) ?? throw new AssistantToolException($"Falta o campo {name}.");

    public static DateOnly ParseDate(string value, string fieldName) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new AssistantToolException($"O campo {fieldName} deve ser uma data válida no formato AAAA-MM-DD.");

    public static TimeOnly ParseTime(string value) =>
        TimeOnly.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : throw new AssistantToolException("O campo time deve ser uma hora válida no formato HH:mm (24 horas).");

    public static DateTime LocalNow(TimeProvider clock, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTime(clock.GetUtcNow(), timeZone).DateTime;
}
