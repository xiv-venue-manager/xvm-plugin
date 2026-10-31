namespace VenueManager;

internal static class Plugin
{
    internal static StubLog Log { get; } = new();
    internal static StubChat Chat { get; } = new();
}

internal sealed class StubLog
{
    internal List<string> Warnings { get; } = new();

    internal void Warning(string message) => Warnings.Add(message);
}

internal sealed class StubChat
{
    internal void Print(string message)
    {
    }
}
