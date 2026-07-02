namespace Zits.Ui;

/// <summary>
/// The class-merge helper (the styled layer's <c>cn()</c>). clsx-style: joins non-empty
/// class groups and de-duplicates tokens, so a component's base classes combine
/// with consumer-supplied <c>class</c> overrides. For full Tailwind conflict
/// resolution (e.g. a later <c>p-4</c> beating an earlier <c>p-2</c>), add the
/// <c>TailwindMerge.NET</c> package and pipe the result through <c>TwMerge.Merge</c>.
/// </summary>
public static class Cn
{
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

        return string.Join(' ', tokens);
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
