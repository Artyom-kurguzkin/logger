using Microsoft.AspNetCore.Components;

namespace AkLogger.Components;

/// <summary>
/// The base of every component we write: the one place that decides when a component draws again.
/// By default it draws exactly as <see cref="ComponentBase"/> does (after every event, after every parameter set), so deriving from it changes nothing;
/// a component opts in to less drawing with <see cref="Strict"/> and <see cref="RenderKey"/>.
/// </summary>
public abstract class AkComponent : ComponentBase, IHandleEvent
{
    /// <summary>
    /// True: an event does not draw the component by itself. The handler calls <see cref="Invalidate"/> when it changed something the component shows.
    /// False (the default): as Blazor does, a draw after every handler.
    /// </summary>
    protected virtual bool Strict => false;

    /// <summary>
    /// What a parameter-set draw depends on. Blazor sets the parameters again whenever the parent draws; while this key stays equal (Equals) the
    /// component is not drawn for it. Null (the default) draws on every parameter set, as Blazor does.
    /// </summary>
    protected virtual object? RenderKey => null;

    /// <summary>Draws the component after state it shows changed outside a legacy event (a strict handler, a timer, an event from another component). Safe from any thread.</summary>
    protected void Invalidate() => _ = InvokeAsync(StateHasChanged);

    // ---- events ----

    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg) =>
        Strict ? HandleStrictAsync(callback, arg) : HandleLegacyAsync(callback, arg);

    // ComponentBase's own handling: a draw after the synchronous part, and one more when an async handler completes.
    private Task HandleLegacyAsync(EventCallbackWorkItem callback, object? arg)
    {
        Task task = callback.InvokeAsync(arg);
        bool awaitTask = task.Status != TaskStatus.RanToCompletion && task.Status != TaskStatus.Canceled;
        StateHasChanged();
        return awaitTask ? DrawAfterAsync(task) : Task.CompletedTask;
    }

    private async Task DrawAfterAsync(Task task)
    {
        try { await task; }
        catch { if (task.IsCanceled) return; throw; }
        StateHasChanged();
    }

    // No draw. IHandleEvent skips the error boundary, so a failing handler is handed to it explicitly (dotnet/aspnetcore#54543).
    private async Task HandleStrictAsync(EventCallbackWorkItem callback, object? arg)
    {
        try { await callback.InvokeAsync(arg); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { await DispatchExceptionAsync(ex); }
    }

    // ---- parameters ----

    private bool parametersPass, neverSet = true;
    private object? drawnKey;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        bool first = neverSet;
        neverSet = false;
        parametersPass = !first;
        Task task;
        try { task = base.SetParametersAsync(parameters); }
        finally { parametersPass = false; }
        if (first) drawnKey = RenderKey;   // the first draw does not ask ShouldRender
        return task;
    }

    /// <summary>Components with their own rule override this and call the base first (or skip it, for a rule that needs no key).</summary>
    protected override bool ShouldRender()
    {
        object? key = RenderKey;
        if (parametersPass && key is not null && key.Equals(drawnKey)) return false;
        drawnKey = key;
        return true;
    }
}

/// <summary>A layout: <see cref="LayoutComponentBase"/>'s <c>Body</c>, on <see cref="AkComponent"/>.</summary>
public abstract class AkLayout : AkComponent
{
    [Parameter] public RenderFragment? Body { get; set; }
}
