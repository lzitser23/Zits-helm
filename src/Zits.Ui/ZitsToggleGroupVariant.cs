namespace Zits.Ui;

/// <summary>
/// The variant/size context a <c>ZitsToggleGroup</c> cascades to its items.
/// An item falls back to these when its own Variant/Size are left at the default.
/// </summary>
public sealed record ZitsToggleGroupVariant(string Variant, string Size);
