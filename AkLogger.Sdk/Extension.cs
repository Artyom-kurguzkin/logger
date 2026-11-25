namespace AkLogger.Sdk;

/// <summary>One thing that adds to the app: a built-in feature, or (later) the adapter of an out-of-process extension. It registers what it contributes once, at startup.</summary>
public interface IExtension
{
    void Register(IExtensionHost host);
}

/// <summary>What an extension registers into. Each Add refuses a second descriptor with the same id (the same name for a query function).</summary>
public interface IExtensionHost
{
    void AddSource(SourceProvider provider);
    void AddParser(ParserProvider provider);
    void AddView(ViewDescriptor view);
    void AddQueryFunction(QueryFunction function);
    void AddAlertAction(AlertAction action);
    /// <summary>An MCP tool class the agent endpoint will serve (not used before the MCP step).</summary>
    void AddTools(Type mcpToolType);
}

/// <summary>
/// One setting a provider asks for; the Settings page renders its form from these. <paramref name="Secret"/>: a password input, never shown back.
/// (Modelled on the task manager's StorageField, without its repository/computer split.)
/// </summary>
public sealed record SettingsField(string Key, string Label, string Placeholder = "", bool Secret = false, bool Optional = false);

/// <summary>A kind of log source (a file to tail, a folder, a socket): what it is called, the fields its settings ask for, and how to open one from the saved values.</summary>
public sealed record SourceProvider(string Id, string Name, IReadOnlyList<SettingsField> Fields, Func<IReadOnlyDictionary<string, string>, ILogSource> Open);

/// <summary>A line format (JSON lines, logfmt, ...): turns one raw line into a record, or null when the line is not this format.</summary>
public sealed record ParserProvider(string Id, string Name, Func<string, LogRecord?> TryParse);

/// <summary>
/// A whole pane the app can show. <paramref name="Icon"/> is the data of one svg path in a 16x16 box (the side menu draws it); <paramref name="Route"/> is the
/// first segment of the address ("" is the start page). The page is a Blazor <paramref name="Component"/> (built-ins) or, for an extension, a
/// <paramref name="ResourceUri"/> (ui://...). <paramref name="Nav"/> false: reachable by address only. The side menu lists views by <paramref name="Order"/>, then title.
/// </summary>
public sealed record ViewDescriptor(string Id, string Title, string Icon, string Route, Type? Component, string? ResourceUri = null, bool Nav = true, int Order = 0);

/// <summary>A function the query language gains (e.g. <c>level_rank(level)</c>).</summary>
public sealed record QueryFunction(string Name, string Description, Func<object?[], object?> Invoke);   // ponytail: shape fixed when the query step (5) first uses it

/// <summary>What an alert can do when it fires (a toast, a webhook, a script); the fields are asked for in Settings.</summary>
public sealed record AlertAction(string Id, string Name, IReadOnlyList<SettingsField> Fields, Func<IReadOnlyDictionary<string, string>, string, Task> Fire);   // ponytail: shape fixed when the alerts step (5) first uses it
