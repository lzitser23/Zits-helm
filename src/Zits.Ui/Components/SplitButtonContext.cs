namespace Zits.Ui;

/// <summary>
/// Cascaded from a <c>ZitsSplitButton</c> to its action + trigger so both share one
/// Variant/Size/Disabled without re-declaring them per part.
/// </summary>
public sealed record SplitButtonContext(string Variant, string Size, bool Disabled);

/// <summary>
/// The button cva strings the SplitButton trigger needs. The action is a real
/// <c>ZitsButton</c>; the trigger styles the menu-trigger button directly (a button cannot
/// nest a button), so these tokens are copied verbatim from <c>ZitsButton</c> to keep the two
/// halves of the seam visually identical.
/// </summary>
internal static class SplitButtonStyles
{
    public const string Base =
        "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-md text-sm font-medium ring-offset-background transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 data-[disabled]:pointer-events-none data-[disabled]:opacity-50";

    public static string Variant(string variant) => variant switch
    {
        "destructive" => "bg-destructive text-destructive-foreground hover:bg-destructive/90",
        "outline" => "border border-input bg-background hover:bg-accent hover:text-accent-foreground",
        "secondary" => "bg-secondary text-secondary-foreground hover:bg-secondary/80",
        "ghost" => "hover:bg-accent hover:text-accent-foreground",
        "link" => "text-primary underline-offset-4 hover:underline",
        _ => "bg-primary text-primary-foreground hover:bg-primary/90",
    };

    /// <summary>The trigger keeps the action's height but stays a narrow chevron (px-2).</summary>
    public static string TriggerSize(string size) => size switch
    {
        "sm" => "h-8 px-2 text-xs",
        "lg" => "h-10 px-3",
        _ => "h-9 px-2",
    };
}
