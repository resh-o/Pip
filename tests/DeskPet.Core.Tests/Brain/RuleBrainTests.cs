using DeskPet.Core.Brain;
using DeskPet.Core.Config;
using DeskPet.Core.Models;
using static DeskPet.Core.Tests.Brain.BrainTestData;

namespace DeskPet.Core.Tests.Brain;

public sealed class RuleBrainTests
{
    private readonly RuleBrain _brain = new(new StaticSettings(new Settings()));
    private static readonly Goal Thesis = new("Write my thesis chapter on photosynthesis");

    [Theory]
    [InlineData("Code", "main.cs - Visual Studio Code")]
    [InlineData("devenv", "Solution")]
    [InlineData("WINWORD.EXE", "Document1")]
    public void OnTaskProcessOrTitleIsOnTask(string process, string title) =>
        Assert.Equal(Verdict.OnTask, _brain.Judge(Goal.None, Window(process, title)));

    [Theory]
    [InlineData("chrome", "Funny cats - YouTube - Google Chrome")]
    [InlineData("firefox", "reddit: the front page")]
    [InlineData("Steam", "Library")]
    public void DistractionKeywordIsDistraction(string process, string title) =>
        Assert.Equal(Verdict.Distraction, _brain.Judge(Goal.None, Window(process, title)));

    [Fact]
    public void DistractionTitleBeatsOnTaskProcess() =>
        Assert.Equal(Verdict.Distraction, _brain.Judge(Thesis, Window("Code", "YouTube preview")));

    [Fact]
    public void GoalWordInTitleIsOnTask() =>
        Assert.Equal(Verdict.OnTask, _brain.Judge(Thesis, Window("chrome", "Photosynthesis - Wikipedia")));

    [Fact]
    public void ShortAndStopWordsFromGoalAreIgnored()
    {
        var goal = new Goal("Work on my app with this");

        Assert.Equal(Verdict.Neutral, _brain.Judge(goal, Window("chrome", "on my app with this")));
    }

    [Fact]
    public void GoalWordMustBeWholeWord() =>
        Assert.Equal(Verdict.Neutral, _brain.Judge(new Goal("learn rust"), Window("chrome", "Trustpilot reviews")));

    [Fact]
    public void DistractionMentionedInGoalIsNotADistraction()
    {
        var goal = new Goal("Edit my YouTube channel trailer");

        Assert.Equal(Verdict.OnTask, _brain.Judge(goal, Window("chrome", "Channel content - YouTube Studio")));
    }

    [Fact]
    public void UnknownWindowIsNeutral() =>
        Assert.Equal(Verdict.Neutral, _brain.Judge(Thesis, Window("explorer", "Downloads")));

    [Fact]
    public void OnTaskKeywordNeedsWholeWord() =>
        Assert.Equal(Verdict.Neutral, _brain.Judge(Goal.None, Window("scanner", "Barcode generator")));

    [Fact]
    public async Task JudgeAsyncReturnsOneVerdictPerWindowInOrder()
    {
        var windows = new[] { Window("explorer", "x"), Window("Steam", "Store"), Window("Code", "a.cs") };

        var verdicts = await _brain.JudgeAsync(Thesis, windows, CancellationToken.None);

        Assert.Equal([Verdict.Neutral, Verdict.Distraction, Verdict.OnTask], verdicts);
    }

    [Fact]
    public void UsesCurrentSettings()
    {
        var settings = new StaticSettings(new Settings());
        var brain = new RuleBrain(settings);
        settings.Current = new Settings { DistractionKeywords = ["Solitaire"] };

        Assert.Equal(Verdict.Distraction, brain.Judge(Goal.None, Window("sol", "Solitaire")));
    }
}
