namespace Stanza.TerminalGui;

public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error,
}

public static class StanzaConfig
{
    public static ILogger? Logger { get; set; }

    public static void Trace(
        string message,
        LogLevel level = LogLevel.Debug,
        [System.Runtime.CompilerServices.CallerFilePath] string? category = null
    )
    {
        Logger?.Log(level, message, Path.GetFileNameWithoutExtension(category) ?? "Stanza");
    }

    public static void Error(
        Exception exception,
        string message,
        [System.Runtime.CompilerServices.CallerFilePath] string? category = null
    )
    {
        Logger?.LogError(
            exception,
            message,
            Path.GetFileNameWithoutExtension(category) ?? "Stanza"
        );
    }
}
