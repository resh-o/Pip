using DeskPet.Core.Brain;
using DeskPet.Core.Models;

namespace DeskPet.Core.Tests.Brain;

public sealed class VerdictJsonParserTests
{
    private static string Response(string input, string stopReason = "tool_use", string name = "classify_windows") => $$$"""
        {"id":"msg_1","type":"message","role":"assistant","model":"m",
         "content":[{"type":"tool_use","id":"toolu_1","name":"{{{name}}}","input":{{{input}}}}],
         "stop_reason":"{{{stopReason}}}","usage":{"input_tokens":1,"output_tokens":1}}
        """;

    private static string Items(string items) => $$$"""{"verdicts":[{{{items}}}]}""";

    [Fact]
    public void ParsesAllVerdicts()
    {
        var json = Response(Items("""{"id":0,"verdict":"on_task"},{"id":1,"verdict":"neutral"},{"id":2,"verdict":"distraction"}"""));

        Assert.True(VerdictJsonParser.TryParse(json, 3, out var verdicts));
        Assert.Equal([Verdict.OnTask, Verdict.Neutral, Verdict.Distraction], verdicts);
    }

    [Fact]
    public void OrderOfItemsDoesNotMatter()
    {
        var json = Response(Items("""{"id":1,"verdict":"distraction"},{"id":0,"verdict":"on_task"}"""));

        Assert.True(VerdictJsonParser.TryParse(json, 2, out var verdicts));
        Assert.Equal([Verdict.OnTask, Verdict.Distraction], verdicts);
    }

    [Fact]
    public void FindsToolUseAfterTextBlock()
    {
        const string json = """
            {"content":[{"type":"text","text":"Sure"},
              {"type":"tool_use","id":"t","name":"classify_windows","input":{"verdicts":[{"id":0,"verdict":"neutral"}]}}],
             "stop_reason":"tool_use"}
            """;

        Assert.True(VerdictJsonParser.TryParse(json, 1, out var verdicts));
        Assert.Equal([Verdict.Neutral], verdicts);
    }

    [Fact]
    public void MissingIdsStayNull()
    {
        Assert.True(VerdictJsonParser.TryParse(Response(Items("""{"id":1,"verdict":"on_task"}""")), 3, out var verdicts));
        Assert.Equal([null, Verdict.OnTask, null], verdicts);
    }

    [Fact]
    public void DuplicateIdsKeepTheFirst()
    {
        var json = Response(Items("""{"id":0,"verdict":"on_task"},{"id":0,"verdict":"distraction"}"""));

        Assert.True(VerdictJsonParser.TryParse(json, 1, out var verdicts));
        Assert.Equal([Verdict.OnTask], verdicts);
    }

    [Theory]
    [InlineData("""{"id":-1,"verdict":"on_task"}""")]
    [InlineData("""{"id":2,"verdict":"on_task"}""")]
    [InlineData("""{"id":99999999999,"verdict":"on_task"}""")]
    [InlineData("""{"id":0.5,"verdict":"on_task"}""")]
    [InlineData("""{"id":"0","verdict":"on_task"}""")]
    [InlineData("""{"id":null,"verdict":"on_task"}""")]
    [InlineData("""{"verdict":"on_task"}""")]
    [InlineData("""{"id":0,"verdict":"ON_TASK"}""")]
    [InlineData("""{"id":0,"verdict":"productive"}""")]
    [InlineData("""{"id":0,"verdict":1}""")]
    [InlineData("""{"id":0}""")]
    [InlineData("\"on_task\"")]
    [InlineData("null")]
    [InlineData("[0,\"on_task\"]")]
    public void InvalidItemsAreSkippedWithoutThrowing(string item)
    {
        var json = Response(Items(item + """,{"id":1,"verdict":"distraction"}"""));

        Assert.True(VerdictJsonParser.TryParse(json, 2, out var verdicts));
        Assert.Equal([null, Verdict.Distraction], verdicts);
    }

    public static TheoryData<string?> MalformedBodies => new()
    {
        null,
        "",
        "   ",
        "not json",
        "{",
        """{"content":[{"type":"tool_use","name":"classify_windows","input":{"verdicts":[{"id":0,""",
        "[]",
        "42",
        "\"string\"",
        "null",
        """{"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}""",
        """{"content":[{"type":"text","text":"{\"verdicts\":[{\"id\":0,\"verdict\":\"on_task\"}]}"}],"stop_reason":"end_turn"}""",
        """{"content":[],"stop_reason":"end_turn"}""",
        """{"content":null}""",
        """{"content":{"type":"tool_use"}}""",
        """{"stop_reason":"tool_use"}""",
        """{"content":[null, 1, "x", []]}""",
        """{"content":[{"type":"tool_use","name":"other_tool","input":{"verdicts":[{"id":0,"verdict":"on_task"}]}}]}""",
        """{"content":[{"type":"tool_use","name":"classify_windows"}]}""",
        """{"content":[{"type":"tool_use","name":"classify_windows","input":"verdicts"}]}""",
        """{"content":[{"type":"tool_use","name":"classify_windows","input":{}}]}""",
        """{"content":[{"type":"tool_use","name":"classify_windows","input":{"verdicts":{"0":"on_task"}}}]}""",
        """{"content":[{"type":"tool_use","name":"classify_windows","input":{"verdicts":null}}]}""",
        """{"content":[{"type":1,"name":"classify_windows","input":{"verdicts":[]}}]}""",
        """{"content":[{"type":"tool_use","name":"classify_windows","input":{"verdicts":[{"id":0,"verdict":"on_task"}]}}],"stop_reason":"max_tokens"}""",
        new string('[', 10_000),
    };

    [Theory]
    [MemberData(nameof(MalformedBodies))]
    public void MalformedBodiesReturnFalseWithAllNulls(string? json)
    {
        Assert.False(VerdictJsonParser.TryParse(json, 2, out var verdicts));
        Assert.Equal([null, null], verdicts);
    }

    [Fact]
    public void WrongToolNameReturnsFalse() =>
        Assert.False(VerdictJsonParser.TryParse(Response(Items("""{"id":0,"verdict":"on_task"}"""), name: "classify"), 1, out _));

    [Fact]
    public void MaxTokensStopReturnsFalse() =>
        Assert.False(VerdictJsonParser.TryParse(Response(Items("""{"id":0,"verdict":"on_task"}"""), stopReason: "max_tokens"), 1, out _));

    [Fact]
    public void EmptyVerdictListIsWellFormedButAllNull()
    {
        Assert.True(VerdictJsonParser.TryParse(Response(Items("")), 2, out var verdicts));
        Assert.Equal([null, null], verdicts);
    }

    [Fact]
    public void NegativeExpectedCountYieldsEmptyArray()
    {
        Assert.True(VerdictJsonParser.TryParse(Response(Items("""{"id":0,"verdict":"on_task"}""")), -3, out var verdicts));
        Assert.Empty(verdicts);
    }
}
