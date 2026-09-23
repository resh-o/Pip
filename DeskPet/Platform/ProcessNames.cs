namespace DeskPet.Platform;

/// <summary>Process id → executable name (no ".exe"), cached because enumeration asks repeatedly.</summary>
internal sealed class ProcessNames
{
    private const int MaxCached = 256;
    private readonly Dictionary<uint, string> _cache = new();
    private readonly object _gate = new();

    public string Of(uint processId)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(processId, out var name))
                return name;
            if (_cache.Count >= MaxCached)
                _cache.Clear(); // ids get reused, so a periodic reset also drops stale names
            name = Query(processId);
            _cache[processId] = name;
            return name;
        }
    }

    private static unsafe string Query(uint processId)
    {
        nint process = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, processId);
        if (process == 0)
            return string.Empty;
        try
        {
            char* buffer = stackalloc char[1024];
            uint size = 1024;
            if (!NativeMethods.QueryFullProcessImageName(process, 0, buffer, ref size))
                return string.Empty;
            return Path.GetFileNameWithoutExtension(new ReadOnlySpan<char>(buffer, (int)size)).ToString();
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }
}
