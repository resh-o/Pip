using DeskPet.Core.Models;

namespace DeskPet.Core.Abstractions;

public interface IBrain
{
    /// <summary>Returns one verdict per input window, in the same order.</summary>
    Task<IReadOnlyList<Verdict>> JudgeAsync(Goal goal, IReadOnlyList<WindowInfo> windows, CancellationToken cancellationToken);
}
