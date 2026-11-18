using System.Text;
using System.Text.Json;
using PersonalAssistant.Core.Assistant;

namespace PersonalAssistant.Core.Shopping;

internal static class ShoppingText
{
    public static string Describe(ShoppingItem item) =>
        string.IsNullOrWhiteSpace(item.Quantity) ? item.Name : $"{item.Name} ({item.Quantity})";

    public static string Count(int count) => count == 1 ? "1 item" : $"{count} itens";

    public static string FormatList(IReadOnlyList<ShoppingItem> items)
    {
        var result = new StringBuilder($"Lista de compras da casa, {Count(items.Count)}:");

        foreach (var section in items.GroupBy(item => item.Section).OrderBy(group => ShoppingSections.Order(group.Key)))
        {
            result.Append($"\n\n{ShoppingSections.DisplayName(section.Key)}:");

            foreach (var item in section)
            {
                result.Append($"\n- [id {item.Id}] {Describe(item)}, pedido por {item.AddedBy}");
            }
        }

        return result.ToString();
    }

    public static IReadOnlyList<long> ReadIds(JsonElement input, string name)
    {
        if (input.ValueKind != JsonValueKind.Object
            || !input.TryGetProperty(name, out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new AssistantToolException($"O campo {name} deve ser uma lista de ids.");
        }

        return value.EnumerateArray()
            .Select(id => id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var number)
                ? number
                : throw new AssistantToolException($"O campo {name} deve ter só números."))
            .ToList();
    }
}
