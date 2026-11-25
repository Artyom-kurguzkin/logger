namespace AkLogger.AppState;

/// <summary>One pane of the window: an address (base-relative: "", "settings", ...) shown by its own view.</summary>
public sealed class Pane
{
    internal Pane(int id, string path) { Id = id; Path = path; }
    public int Id { get; }
    public string Path { get; internal set; }
}

/// <summary>
/// The panes of the window, side by side. One is active: the window's address and its side menu follow it. At most <see cref="Max"/>; never fewer than one.
/// ponytail: nothing splits the window yet (the task manager binds Alt+\ and Ctrl+W to Split and Close in app.js); one pane until a feature asks for two.
/// </summary>
public sealed class Panes
{
    public const int Max = 4;
    private readonly List<Pane> all;
    private int nextId = 1;

    public Panes()
    {
        all = [new Pane(nextId++, "")];
        Active = all[0];
    }

    public IReadOnlyList<Pane> All => all;
    public Pane Active { get; private set; }

    /// <summary>Something changed: the panes, which one is active, or an address.</summary>
    public event Action? Changed;

    /// <summary>Adds a pane to the right of the active one, showing the same view, and makes it active. A fifth is refused (null).</summary>
    public Pane? Split()
    {
        if (all.Count >= Max) return null;
        Pane added = new(nextId++, Active.Path);
        all.Insert(all.IndexOf(Active) + 1, added);
        Active = added;
        Changed?.Invoke();
        return added;
    }

    /// <summary>Closes a pane; the last one stays. The neighbour becomes active when it was the active one.</summary>
    public bool Close(Pane pane)
    {
        int at = all.IndexOf(pane);
        if (at < 0 || all.Count == 1) return false;
        all.RemoveAt(at);
        if (Active == pane) Active = all[Math.Min(at, all.Count - 1)];
        Changed?.Invoke();
        return true;
    }

    public void Activate(Pane pane)
    {
        if (Active == pane || !all.Contains(pane)) return;
        Active = pane;
        Changed?.Invoke();
    }

    public void Navigate(Pane pane, string path)
    {
        if (pane.Path == path || !all.Contains(pane)) return;
        pane.Path = path;
        Changed?.Invoke();
    }

    /// <summary>The pane's place, counting from 1.</summary>
    public int Number(Pane pane) => all.IndexOf(pane) + 1;
}
