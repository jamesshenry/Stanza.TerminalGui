namespace Stanza.TerminalGui;

/// <summary>
/// A lightweight logging interface for Stanza binding and lifecycle events.
/// </summary>
public interface ILogger
{
    /// <summary>
    /// Log the received message.
    /// </summary>
    /// <param name="message"></param>
    void Log(LogLevel level, string message, string category);

    // Explicitly forces exceptions to be handled as Errors
    void LogError(Exception exception, string message, string category);
}
