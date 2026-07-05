using System.Text;
using Zits.Ui.Theming;

// Writes the generated zits-theme.css (see ThemeStylesheet). Deterministic: running
// twice always produces byte-identical output. Usage, from the repo root:
//
//   dotnet run --project src/Zits.Ui.CssGen [output-path]
//
// The default output path is the committed stylesheet next to the styled layer.

var outputPath = args.Length > 0
    ? args[0]
    : Path.Combine("src", "Zits.Ui", "wwwroot", "zits-theme.css");

var css = ThemeStylesheet.Generate();
File.WriteAllText(outputPath, css, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
Console.WriteLine($"Wrote {css.Length} chars to {Path.GetFullPath(outputPath)}");
