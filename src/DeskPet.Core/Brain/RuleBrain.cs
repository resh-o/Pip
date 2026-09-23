using System.Text.RegularExpressions;
using DeskPet.Core.Abstractions;
using DeskPet.Core.Config;
using DeskPet.Core.Models;

namespace DeskPet.Core.Brain;

/// <summary>Offline keyword heuristics; the fallback whenever the API can't or mustn't be used.</summary>
public sealed partial class RuleBrain : IBrain
{
    private const int MinGoalWordLetters = 4;

    // Common long function words that would otherwise make almost any title "on task".
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "about", "after", "also", "been", "before", "from", "have", "into", "just", "like", "make",
        "more", "some", "than", "that", "their", "them", "then", "there", "these", "this", "want",
        "what", "when", "with", "will", "your",
    };

    private readonly ISettings _settings;

    public RuleBrain(ISettings settings) => _settings = settings;

    public Task<IReadOnlyList<Verdict>> JudgeAsync(Goal goal, IReadOnlyList<WindowInfo> windows, CancellationToken cancellationToken)
    {
        var rules = new Rules(_settings.Current, goal);
        IReadOnlyList<Verdict> verdicts = windows.Select(rules.Judge).ToArray();
        return Task.FromResult(verdicts);
    }

    public Verdict Judge(Goal goal, WindowInfo window) => new Rules(_settings.Current, goal).Judge(window);

    private sealed class Rules
    {
        private readonly Settings _settings;
        private readonly string[] _goalWords;
        private readonly string[] _distractions;

        public Rules(Settings settings, Goal goal)
        {
            _settings = settings;
            _goalWords = GoalWords(goal.Text);
            // A goal like "edit my YouTube channel" makes YouTube work, not leisure.
            _distractions = settings.DistractionKeywords.Where(k => !TextMatch.ContainsWord(goal.Text, k)).ToArray();
        }

        public Verdict Judge(WindowInfo window)
        {
            if (_distractions.Any(k => TextMatch.ContainsWord(window.Title, k)))
            {
                return Verdict.Distraction;
            }

            if (_goalWords.Any(w => TextMatch.ContainsWord(window.Title, w))
                || _settings.OnTaskKeywords.Any(k => Matches(window, k)))
            {
                return Verdict.OnTask;
            }

            return _distractions.Any(k => TextMatch.SameProcess(window.ProcessName, k)) ? Verdict.Distraction : Verdict.Neutral;
        }

        private static bool Matches(WindowInfo window, string keyword) =>
            TextMatch.SameProcess(window.ProcessName, keyword) || TextMatch.ContainsWord(window.Title, keyword);

        private static string[] GoalWords(string goal) =>
            NonWord().Split(goal)
                .Where(w => w.Count(char.IsLetter) >= MinGoalWordLetters && !StopWords.Contains(w))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NonWord();
}
