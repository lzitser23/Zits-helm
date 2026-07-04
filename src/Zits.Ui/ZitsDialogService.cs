using Microsoft.AspNetCore.Components;

namespace Zits.Ui;

/// <summary>
/// The imperative dialog store for the styled helm. <c>@inject</c>-able and registered
/// <b>scoped</b> by <c>AddZitsUi()</c> (mirroring how the brain's <c>AddNavius()</c> registers
/// <c>ToastManager</c>). It lets callers <b>await a dialog like a modal function call</b>:
/// <c>if (await Dialog.ConfirmAsync(...)) { ... }</c>.
///
/// It does not own any overlay machinery: a single <c>&lt;ZitsDialogOutlet /&gt;</c> mounted near
/// the app root renders one <see cref="ZitsDialog"/> / <see cref="ZitsAlertDialog"/> per queued
/// entry, so every dialog rides the brain's real focus-trap / scroll-lock / dismiss-layer
/// lifecycle (no bypass). The service only holds the queue and the awaited results.
/// </summary>
public sealed class ZitsDialogService
{
    private readonly List<DialogEntry> _entries = new();

    /// <summary>The live dialog entries the outlet renders (oldest first).</summary>
    public IReadOnlyList<DialogEntry> Entries => _entries;

    /// <summary>Raised after any mutation so the outlet re-renders.</summary>
    public event Action? Changed;

    /// <summary>
    /// Show a confirm dialog (over <see cref="ZitsAlertDialog"/>: modal, outside-click never
    /// closes). Resolves <c>true</c> if the user confirms, <c>false</c> on cancel/Escape.
    /// </summary>
    public async Task<bool> ConfirmAsync(string title, string description, ConfirmOptions? options = null)
    {
        var o = options ?? new ConfirmOptions();
        var entry = new DialogEntry
        {
            Kind = DialogKind.Confirm,
            Title = title,
            Description = description,
            ConfirmLabel = o.ConfirmLabel,
            CancelLabel = o.CancelLabel,
            Destructive = o.Destructive,
            DefaultResult = false,
        };
        Add(entry);
        var result = await entry.Completion.Task;
        return result is true;
    }

    /// <summary>
    /// Show an alert dialog (over <see cref="ZitsAlertDialog"/>) with a single acknowledge
    /// button. Resolves when the user dismisses it.
    /// </summary>
    public async Task AlertAsync(string title, string description, AlertOptions? options = null)
    {
        var o = options ?? new AlertOptions();
        var entry = new DialogEntry
        {
            Kind = DialogKind.Alert,
            Title = title,
            Description = description,
            OkLabel = o.OkLabel,
            DefaultResult = null,
        };
        Add(entry);
        await entry.Completion.Task;
    }

    /// <summary>
    /// Show arbitrary content (a form, a picker, anything) inside a <see cref="ZitsDialog"/>
    /// the outlet renders, and await its result. The content is a template over a
    /// <see cref="DialogHandle{T}"/> so buttons inside can call <c>handle.Close(result)</c> /
    /// <c>handle.Cancel()</c>. Dismissing via Escape / outside resolves with <c>default</c>.
    /// </summary>
    public async Task<T?> ShowAsync<T>(RenderFragment<DialogHandle<T>> content, DialogOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        var o = options ?? new DialogOptions();
        var id = Guid.NewGuid().ToString("N");
        var handle = new DialogHandle<T>(this, id);
        var entry = new DialogEntry
        {
            Id = id,
            Kind = DialogKind.Custom,
            Modal = o.Modal,
            Content = content(handle),
            DefaultResult = default(T),
        };
        Add(entry);
        var result = await entry.Completion.Task;
        return result is T typed ? typed : default;
    }

    /// <summary>
    /// Resolve a dialog with <paramref name="result"/>, flip it closed (the outlet animates it
    /// out, then removes it), and deliver the awaited value. Idempotent: the first resolution
    /// wins, so an explicit button click beats a later dismiss.
    /// </summary>
    public void Resolve(string id, object? result)
    {
        var entry = _entries.FirstOrDefault(e => e.Id == id);
        if (entry is null || entry.Resolved)
        {
            return;
        }

        entry.Resolved = true;
        entry.Open = false;
        entry.Completion.TrySetResult(result);
        Changed?.Invoke();
    }

    /// <summary>Remove a resolved entry from the queue (called by the outlet after its exit animation).</summary>
    public void Remove(string id)
    {
        var entry = _entries.FirstOrDefault(e => e.Id == id);
        if (entry is null)
        {
            return;
        }

        // Safety net: if it is somehow removed before resolving, complete it with the default so
        // no awaiter is left hanging forever.
        if (!entry.Resolved)
        {
            entry.Resolved = true;
            entry.Completion.TrySetResult(entry.DefaultResult);
        }

        _entries.Remove(entry);
        Changed?.Invoke();
    }

    private void Add(DialogEntry entry)
    {
        _entries.Add(entry);
        Changed?.Invoke();
    }
}

/// <summary>Which styled surface an entry renders as.</summary>
public enum DialogKind
{
    /// <summary>A confirm dialog (Cancel + Action) over <see cref="ZitsAlertDialog"/>.</summary>
    Confirm,

    /// <summary>An alert dialog (single acknowledge button) over <see cref="ZitsAlertDialog"/>.</summary>
    Alert,

    /// <summary>Arbitrary content over <see cref="ZitsDialog"/>.</summary>
    Custom,
}

/// <summary>
/// One dialog in the <see cref="ZitsDialogService"/> queue. A class (not a record) because its
/// transient fields (<see cref="Open"/>, <see cref="Resolved"/>) mutate in place while it lives.
/// </summary>
public sealed class DialogEntry
{
    /// <summary>Stable id, used as the render <c>@key</c> and for <c>Resolve</c>/<c>Remove</c>.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Which styled surface to render.</summary>
    public DialogKind Kind { get; init; }

    /// <summary>True while open; flipped false by <see cref="ZitsDialogService.Resolve"/> to start the exit.</summary>
    public bool Open { get; set; } = true;

    /// <summary>Whether the awaited result has already been delivered (guards double-resolution).</summary>
    public bool Resolved { get; set; }

    /// <summary>Title text (confirm/alert).</summary>
    public string? Title { get; init; }

    /// <summary>Body text (confirm/alert).</summary>
    public string? Description { get; init; }

    /// <summary>Confirm-button label (confirm).</summary>
    public string ConfirmLabel { get; init; } = "Confirm";

    /// <summary>Cancel-button label (confirm).</summary>
    public string CancelLabel { get; init; } = "Cancel";

    /// <summary>Acknowledge-button label (alert).</summary>
    public string OkLabel { get; init; } = "OK";

    /// <summary>Emit <c>data-destructive</c> on the confirm action (a styling passthrough).</summary>
    public bool Destructive { get; init; }

    /// <summary>Modal flag for a custom dialog (passed through to <see cref="ZitsDialog"/>).</summary>
    public bool Modal { get; init; } = true;

    /// <summary>Pre-built content for a custom dialog (already bound to its <see cref="DialogHandle{T}"/>).</summary>
    public RenderFragment? Content { get; init; }

    /// <summary>The value delivered when the dialog is dismissed rather than explicitly resolved.</summary>
    internal object? DefaultResult { get; init; }

    internal TaskCompletionSource<object?> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>A handle passed to custom dialog content so it can resolve the awaited <c>ShowAsync</c> call.</summary>
public sealed class DialogHandle<T>
{
    private readonly ZitsDialogService _service;
    private readonly string _id;

    internal DialogHandle(ZitsDialogService service, string id)
    {
        _service = service;
        _id = id;
    }

    /// <summary>Close the dialog and resolve <c>ShowAsync&lt;T&gt;</c> with <paramref name="result"/>.</summary>
    public void Close(T result) => _service.Resolve(_id, result);

    /// <summary>Close the dialog and resolve with <c>default</c>.</summary>
    public void Cancel() => _service.Resolve(_id, default(T));
}

/// <summary>Options for <see cref="ZitsDialogService.ConfirmAsync"/>.</summary>
public sealed record ConfirmOptions(
    string ConfirmLabel = "Confirm",
    string CancelLabel = "Cancel",
    bool Destructive = false);

/// <summary>Options for <see cref="ZitsDialogService.AlertAsync"/>.</summary>
public sealed record AlertOptions(string OkLabel = "OK");

/// <summary>Options for <see cref="ZitsDialogService.ShowAsync{T}"/>.</summary>
public sealed record DialogOptions(bool Modal = true);
