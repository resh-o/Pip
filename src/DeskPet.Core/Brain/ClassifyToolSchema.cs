using System.Text.Json;

namespace DeskPet.Core.Brain;

/// <summary>Writes the strict classify_windows tool definition and the forced tool_choice.</summary>
internal static class ClassifyToolSchema
{
    public const string ToolName = "classify_windows";

    private const string Description =
        "Report a focus verdict for every listed window, referencing each window by its id.";

    public static void WriteTools(Utf8JsonWriter writer)
    {
        writer.WriteStartArray("tools");
        writer.WriteStartObject();
        writer.WriteString("name", ToolName);
        writer.WriteString("description", Description);
        writer.WriteBoolean("strict", true);
        writer.WritePropertyName("input_schema");
        WriteInputSchema(writer);
        writer.WriteEndObject();
        writer.WriteEndArray();
    }

    public static void WriteToolChoice(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("tool_choice");
        writer.WriteString("type", "tool");
        writer.WriteString("name", ToolName);
        writer.WriteEndObject();
    }

    private static void WriteInputSchema(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "object");
        writer.WriteStartObject("properties");
        writer.WriteStartObject("verdicts");
        writer.WriteString("type", "array");
        writer.WritePropertyName("items");
        WriteItemSchema(writer);
        writer.WriteEndObject();
        writer.WriteEndObject();
        WriteClosedObjectTail(writer, "verdicts");
        writer.WriteEndObject();
    }

    private static void WriteItemSchema(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "object");
        writer.WriteStartObject("properties");
        writer.WriteStartObject("id");
        writer.WriteString("type", "integer");
        writer.WriteEndObject();
        writer.WriteStartObject("verdict");
        writer.WriteString("type", "string");
        writer.WriteStartArray("enum");
        foreach (var name in VerdictNames.All)
        {
            writer.WriteStringValue(name);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
        WriteClosedObjectTail(writer, "id", "verdict");
        writer.WriteEndObject();
    }

    private static void WriteClosedObjectTail(Utf8JsonWriter writer, params string[] required)
    {
        writer.WriteStartArray("required");
        foreach (var name in required)
        {
            writer.WriteStringValue(name);
        }

        writer.WriteEndArray();
        writer.WriteBoolean("additionalProperties", false);
    }
}
