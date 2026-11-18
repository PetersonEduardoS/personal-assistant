using System.Globalization;

namespace PersonalAssistant.Core.Shopping;

internal static class Money
{
    /// <summary>Euros the Brazilian way, for example €23,88.</summary>
    public static string Format(decimal value) =>
        "€" + value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
}
