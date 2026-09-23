using System.Threading.Channels;

namespace DeskPet.Platform;

/// <summary>
/// Append-only log at %APPDATA%\DeskPet\deskpet.log. Writes are queued and flushed on a background
/// task so callers on the main thread never touch the disk. Holds no window titles (privacy).
/// </summary>
internal static class DiagnosticLog
{
    private const long MaxBytes = 1_000_000;
    private static readonly Channel<string> Lines = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

    public static string Directory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskPet");

    public static string FilePath { get; } = Path.Combine(Directory, "deskpet.log");

    public static void Start() => _ = Task.Run(DrainAsync);

    public static void Write(string line) =>
        Lines.Writer.TryWrite($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}");

    private static async Task DrainAsync()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
                File.Move(FilePath, FilePath + ".old", overwrite: true);
            await using var writer = new StreamWriter(FilePath, append: true) { AutoFlush = true };
            await foreach (string line in Lines.Reader.ReadAllAsync())
                await writer.WriteLineAsync(line);
        }
        catch (IOException)
        {
            // Diagnostics must never take the pet down; losing the log is acceptable.
        }
    }
}
