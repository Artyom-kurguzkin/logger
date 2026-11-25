namespace AkLogger.Sdk;

/// <summary>One log line, parsed. Everything not named here goes in <paramref name="Attributes"/>.</summary>
public sealed record LogRecord(DateTimeOffset Time, string Level, string Message, string? Source = null, string? TraceId = null, string? SpanId = null,
    IReadOnlyDictionary<string, string>? Attributes = null);

/// <summary>One unit of work in a trace (OpenTelemetry-shaped).</summary>
public sealed record Span(string TraceId, string SpanId, string? ParentId, string Name, DateTimeOffset Start, TimeSpan Duration,
    IReadOnlyDictionary<string, string>? Attributes = null);

/// <summary>What one poll of a source returned: the new records, where to continue from, and whether the source has nothing more to give.</summary>
public sealed record LogBatch(IReadOnlyList<LogRecord> Records, string? Cursor, bool Done);

/// <summary>A place records come from. Polled (a source cannot push): the host passes back the cursor it got last.</summary>
public interface ILogSource
{
    Task<LogBatch> ReadAsync(string? cursor, CancellationToken cancel);
}
