namespace Navius;

/// <summary>
/// The class-merge helper (the styled layer's <c>cn()</c>). clsx-style: joins non-empty
/// class strings and de-duplicates tokens. For full Tailwind conflict resolution
/// (e.g. <c>p-2</c> losing to a later <c>p-4</c>), add the
/// <c>TailwindMerge.NET</c> package and pipe this result through <c>TwMerge.Merge</c>.
/// </summary>
public static class Cn
{
    public static string Class(params string?[] classes)
    {
        var seen = new HashSet<string>();
        var tokens = new List<string>();

        foreach (var group in classes)
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
}
