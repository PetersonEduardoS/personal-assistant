namespace PersonalAssistant.Core.Shopping;

/// <summary>The supermarket sections, in the order the list is shown.</summary>
public static class ShoppingSections
{
    public const string Other = "outros";

    private static readonly (string Key, string Name)[] All =
    [
        ("hortifruti", "Hortifrúti"),
        ("carnes", "Carnes e peixes"),
        ("laticinios", "Laticínios e ovos"),
        ("padaria", "Padaria"),
        ("mercearia", "Mercearia"),
        ("congelados", "Congelados"),
        ("bebidas", "Bebidas"),
        ("limpeza", "Limpeza"),
        ("higiene", "Higiene"),
        (Other, "Outros")
    ];

    public static IReadOnlyList<string> Keys { get; } = All.Select(section => section.Key).ToArray();

    /// <summary>A known section key, or "outros" for anything else.</summary>
    public static string Normalize(string? key)
    {
        var clean = key?.Trim().ToLowerInvariant();

        return All.Any(section => section.Key == clean) ? clean! : Other;
    }

    public static string DisplayName(string key) =>
        All.FirstOrDefault(section => section.Key == key).Name ?? "Outros";

    public static int Order(string key)
    {
        var index = Array.FindIndex(All, section => section.Key == key);

        return index < 0 ? All.Length : index;
    }
}
