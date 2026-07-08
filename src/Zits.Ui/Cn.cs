using TailwindMerge;

namespace Zits.Ui;

/// <summary>
/// The class-merge helper (the styled layer's <c>cn()</c>). shadcn parity (clsx + tailwind-merge):
/// joins non-empty class groups, de-duplicates tokens, then resolves Tailwind conflicts through
/// <c>TailwindMerge.NET</c> so a consumer-supplied <c>class</c> reliably beats a component's base
/// classes (e.g. a later <c>p-4</c> beating an earlier <c>p-2</c>, or <c>w-36</c> beating
/// <c>w-[280px]</c>). The last conflicting token wins, and <see cref="Merge"/> appends the
/// consumer class last, so the consumer override wins.
/// </summary>
public static class Cn
{
    // TwMerge holds no per-call state and guards its LRU cache for concurrent use, so a single
    // shared instance backs every call site. A parameterless construction uses the default config.
    private static readonly TwMerge Tw = new();

    public static string Class(params string?[] groups)
    {
        var seen = new HashSet<string>();
        var tokens = new List<string>();

        foreach (var group in groups)
        {
            if (string.IsNullOrWhiteSpace(group))
            {
                continue;
            }

            foreach (var token in group.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (seen.Add(token))
                {
                    tokens.Add(token);
                }
            }
        }

        // De-dupe keeps non-conflicting tokens stable; TwMerge then drops the losing side of any
        // Tailwind conflict (last token wins). Merge returns null for an empty input.
        return Tw.Merge(string.Join(' ', tokens)) ?? string.Empty;
    }

    /// <summary>
    /// The consumer-supplied <c>class</c> pulled out of a CaptureUnmatchedValues
    /// splat, so a component can merge it with its base/variant classes. Render the
    /// merged result in an explicit <c>class="@..."</c> placed AFTER <c>@attributes</c>
    /// so it wins the duplicate-key override (Blazor: last attribute wins, no throw).
    /// </summary>
    public static string? UserClass(IDictionary<string, object>? attributes)
        => attributes is not null && attributes.TryGetValue("class", out var c) ? c?.ToString() : null;

    /// <summary>
    /// Copy a CaptureUnmatchedValues splat with its <c>class</c> replaced by the
    /// merge of <paramref name="baseGroups"/> + the consumer's class. Splat the
    /// result onto an inner element/component (<c>@attributes="Cn.Merge(Attributes, Base)"</c>)
    /// so styled wrappers forward every consumer attribute/parameter AND a single,
    /// correctly-merged class in one shot.
    /// </summary>
    public static IReadOnlyDictionary<string, object> Merge(IDictionary<string, object>? attributes, params string?[] baseGroups)
    {
        var dict = attributes is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(attributes);

        var all = new List<string?>(baseGroups) { UserClass(attributes) };
        dict["class"] = Class(all.ToArray());
        return dict;
    }
}
