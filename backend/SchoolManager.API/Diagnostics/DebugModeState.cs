namespace SchoolManager.API.Diagnostics;

public sealed class DebugModeState
{
    private readonly object sync = new();
    private DateTimeOffset? enabledUntil;

    public DateTimeOffset? EnabledUntil
    {
        get
        {
            lock (sync)
            {
                if (enabledUntil is not null && enabledUntil <= DateTimeOffset.UtcNow)
                    enabledUntil = null;
                return enabledUntil;
            }
        }
    }

    public bool IsEnabled => EnabledUntil is not null;

    public DateTimeOffset Enable(TimeSpan duration)
    {
        var until = DateTimeOffset.UtcNow.Add(duration);
        lock (sync) enabledUntil = until;
        return until;
    }

    public void Disable()
    {
        lock (sync) enabledUntil = null;
    }
}
