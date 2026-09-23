using DeskPet.Core.Models;

namespace DeskPet.Pet;

/// <summary>
/// One sensing tick, handed from the poller thread to the main thread. ForegroundVerdict is set
/// only when the foreground has just been judged; Known only when windows were re-judged.
/// </summary>
internal sealed record Observation(
    WindowInfo? Foreground,
    Verdict? ForegroundVerdict,
    IReadOnlyList<(WindowInfo Window, Verdict Verdict)>? Known);
