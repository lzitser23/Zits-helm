namespace Zits.Ui.Tests;

/// <summary>Locates repo files from the test output directory.</summary>
internal static class RepoPaths
{
    /// <summary>Walk up from the test bin directory to the repo root.</summary>
    public static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "src", "Zits.Ui", "wwwroot", "zits-ui.css")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent!;
            }
            throw new InvalidOperationException("Could not locate the repo root from " + AppContext.BaseDirectory);
        }
    }

    public static string TokenStylesheet => Path.Combine(Root, "src", "Zits.Ui", "wwwroot", "zits-ui.css");

    public static string CommittedStylesheet => Path.Combine(Root, "src", "Zits.Ui", "wwwroot", "zits-theme.css");
}
