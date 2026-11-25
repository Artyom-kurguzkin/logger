using AkLogger.Sdk;

namespace AkLogger.AppState;

/// <summary>
/// The registry every built-in feature (and, later, every external extension) registers into. The UI reads it instead of switching on fixed names:
/// the side menu lists <see cref="NavViews"/>, a pane shows <see cref="FindView"/>'s component. Filled once at startup, so no locking and no change event.
/// </summary>
public sealed class Extensions : IExtensionHost
{
    private readonly List<SourceProvider> sources = [];
    private readonly List<ParserProvider> parsers = [];
    private readonly List<ViewDescriptor> views = [];
    private readonly List<QueryFunction> functions = [];
    private readonly List<AlertAction> actions = [];
    private readonly List<Type> tools = [];

    public IReadOnlyList<SourceProvider> Sources => sources;
    public IReadOnlyList<ParserProvider> Parsers => parsers;
    public IReadOnlyList<ViewDescriptor> Views => views;
    public IReadOnlyList<QueryFunction> Functions => functions;
    public IReadOnlyList<AlertAction> Actions => actions;
    public IReadOnlyList<Type> Tools => tools;

    public Extensions() { }

    public Extensions(IEnumerable<IExtension> extensions)
    {
        foreach (IExtension extension in extensions) extension.Register(this);
    }

    public void AddSource(SourceProvider provider) => Add(sources, provider, p => p.Id, "source");
    public void AddParser(ParserProvider provider) => Add(parsers, provider, p => p.Id, "parser");
    public void AddView(ViewDescriptor view) => Add(views, view, v => v.Id, "view");
    public void AddQueryFunction(QueryFunction function) => Add(functions, function, f => f.Name, "query function");
    public void AddAlertAction(AlertAction action) => Add(actions, action, a => a.Id, "alert action");
    public void AddTools(Type mcpToolType) => Add(tools, mcpToolType, t => t.FullName!, "tools class");

    private static void Add<T>(List<T> list, T item, Func<T, string> id, string kind)
    {
        string key = id(item);
        if (list.Any(x => id(x) == key)) throw new InvalidOperationException($"A {kind} with the id '{key}' is already registered.");
        list.Add(item);
    }

    /// <summary>The view that owns <paramref name="route"/> (the first segment of an address, case aside), or null.</summary>
    public ViewDescriptor? FindView(string route)
    {
        route = route.Trim('/');
        return views.FirstOrDefault(v => string.Equals(v.Route.Trim('/'), route, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The views the side menu lists, in the order it lists them.</summary>
    public IEnumerable<ViewDescriptor> NavViews => views.Where(v => v.Nav).OrderBy(v => v.Order).ThenBy(v => v.Title, StringComparer.CurrentCulture);
}
