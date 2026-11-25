namespace AkLogger.AppState;

public enum ToastKind { Info, Success, Warning }

public sealed record Toast(int Id, string Text, ToastKind Kind);

/// <summary>A confirm or a one-line prompt; <see cref="Result"/> completes with the typed text / "yes", or null when cancelled.</summary>
public sealed record DialogRequest(string Title, string Text, string Yes, string No, bool IsPrompt, string Placeholder, TaskCompletionSource<string?> Result,
    IReadOnlyList<(string Value, string Label)>? Choices = null, string Value = "");   // Choices: a pick-one dialog, one button per choice, no OK; Value: a prompt's starting text

/// <summary>
/// App-wide dialogs and toasts, rendered by the Overlays component in the layout, and the window's panes. Pages call
/// <see cref="Confirm"/>, <see cref="Prompt"/> and <see cref="Toast"/> and await the answer.
/// </summary>
public sealed class UiService
{
    private readonly List<Toast> toasts = [];
    private int nextToast;

    public event Action? Changed;

    /// <summary>The panes of this window; one when not split.</summary>
    public Panes Panes { get; } = new();

    public DialogRequest? Dialog { get; private set; }

    public IReadOnlyList<Toast> Toasts { get { lock (toasts) return toasts.ToList(); } }

    public async Task<bool> Confirm(string title, string text, string yes = "OK", string no = "Cancel") =>
        await Show(new DialogRequest(title, text, yes, no, false, "", new())) is not null;

    public Task<string?> Prompt(string title, string placeholder, string action, string value = "") =>
        Show(new DialogRequest(title, "", action, "Cancel", true, placeholder, new(), Value: value));

    /// <summary>Asks the user to pick one of the choices; the picked value, or null when cancelled.</summary>
    public Task<string?> Choose(string title, string text, IReadOnlyList<(string Value, string Label)> choices) =>
        Show(new DialogRequest(title, text, "", "Cancel", false, "", new(), choices));

    /// <summary>Closes the open dialog: a value means OK (the prompt text, or "yes" for a confirm), null means cancel.</summary>
    public void Close(string? result)
    {
        DialogRequest? d = Dialog;
        Dialog = null;
        Changed?.Invoke();
        d?.Result.TrySetResult(result);
    }

    /// <summary>How long a toast stays up; <see cref="Timeout.InfiniteTimeSpan"/> keeps it (a test that asserts on toasts sets this, so a busy machine cannot expire one mid-test).</summary>
    public TimeSpan ToastLifetime { get; init; } = TimeSpan.FromMilliseconds(3500);

    public void Toast(string text, ToastKind kind = ToastKind.Info)
    {
        Toast t;
        lock (toasts) toasts.Add(t = new Toast(++nextToast, text, kind));
        Changed?.Invoke();
        if (ToastLifetime == Timeout.InfiniteTimeSpan) return;
        _ = Task.Delay(ToastLifetime).ContinueWith(_ =>
        {
            lock (toasts) toasts.Remove(t);
            Changed?.Invoke();
        }, TaskScheduler.Default);
    }

    private Task<string?> Show(DialogRequest d)
    {
        Dialog?.Result.TrySetResult(null);   // only one dialog at a time
        Dialog = d;
        Changed?.Invoke();
        return d.Result.Task;
    }
}
