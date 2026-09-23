using System.Text.Json;
using DeskPet.Core.Models;

namespace DeskPet.Core.Brain;

/// <summary>
/// Reads verdicts out of a Messages API response body. Never throws: model output and network
/// payloads are untrusted, and every failure simply means "fall back to the rules".
/// </summary>
public static class VerdictJsonParser
{
    /// <summary>
    /// Returns false (all entries null) unless the body is a complete classify_windows tool call.
    /// On true, <paramref name="verdicts"/> has <paramref name="expectedCount"/> slots; ids the model omitted,
    /// put out of range or gave an invalid item stay null. When an id repeats, the first valid item wins,
    /// so a model that "changes its mind" mid-list can't flip an earlier answer.
    /// </summary>
    public static bool TryParse(string? json, int expectedCount, out Verdict?[] verdicts)
    {
        verdicts = new Verdict?[Math.Max(expectedCount, 0)];
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!TryFindVerdictItems(document.RootElement, out var items))
            {
                return false;
            }

            Fill(items, verdicts);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryFindVerdictItems(JsonElement root, out JsonElement items)
    {
        items = default;
        // A max_tokens stop means the tool input may have been cut short, so even a parseable list is suspect.
        if (root.ValueKind != JsonValueKind.Object || HasString(root, "stop_reason", "max_tokens"))
        {
            return false;
        }

        if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var block in content.EnumerateArray())
        {
            if (IsClassifyToolUse(block))
            {
                return block.TryGetProperty("input", out var input)
                    && input.ValueKind == JsonValueKind.Object
                    && input.TryGetProperty("verdicts", out items)
                    && items.ValueKind == JsonValueKind.Array;
            }
        }

        return false;
    }

    private static bool IsClassifyToolUse(JsonElement block) =>
        block.ValueKind == JsonValueKind.Object
        && HasString(block, "type", "tool_use")
        && HasString(block, "name", PromptBuilder.ToolName);

    private static void Fill(JsonElement items, Verdict?[] verdicts)
    {
        foreach (var item in items.EnumerateArray())
        {
            if (TryReadItem(item, verdicts.Length, out var id, out var verdict) && verdicts[id] is null)
            {
                verdicts[id] = verdict;
            }
        }
    }

    private static bool TryReadItem(JsonElement item, int count, out int id, out Verdict verdict)
    {
        id = -1;
        verdict = default;
        return item.ValueKind == JsonValueKind.Object
            && item.TryGetProperty("id", out var idElement)
            && idElement.ValueKind == JsonValueKind.Number
            && idElement.TryGetInt32(out id)
            && id >= 0 && id < count
            && item.TryGetProperty("verdict", out var verdictElement)
            && verdictElement.ValueKind == JsonValueKind.String
            && VerdictNames.TryParse(verdictElement.GetString(), out verdict);
    }

    private static bool HasString(JsonElement element, string property, string expected) =>
        element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.ValueEquals(expected);
}
