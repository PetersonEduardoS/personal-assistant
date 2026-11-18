using System.Globalization;

namespace PersonalAssistant.Core.Shopping;

internal static class Money
{
    /// <summary>Euros the Brazilian way, for example €23,88.</summary>
    public static string Format(decimal value) =>
        "€" + value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>A quantity without trailing zeros, for example 2 or 0,926.</summary>
    public static string Quantity(decimal value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');

    public static string ShortDate(DateOnly date) => date.ToString("dd/MM", CultureInfo.InvariantCulture);
}
