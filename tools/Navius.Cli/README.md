# navius

`navius` is a copy-paste component registry CLI for Blazor and zits/ui.

## Install

```bash
dotnet tool install -g navius --prerelease
```

## Use

```bash
navius list
navius add dialog --to ./src/MyApp --namespace MyApp.Ui
```

By default the installed tool uses its bundled zits/ui registry. During local development or for custom registries, pass `--root` and `--registry` to point at another registry root.

### Styled-only (consume the brain as a package)

`add` normally vendors both layers: the headless Navius brain (files under `Navius/`) and the styled zits/ui components (files under `Zits/`). Pass `--styled-only` to copy just the styled and lib files and consume the brain as the published `Navius.Primitives` NuGet package instead:

```bash
navius add date-picker --styled-only --to ./src/MyApp
```

Every file sourced from the brain (`../navius/...`) is skipped; the styled `Zits*` files and the `cn()` helper are still copied. The `cn()` helper needs the `TailwindMerge.NET` NuGet package (it resolves Tailwind class conflicts so a consumer `class` beats a component's base classes); `add` prints the `dotnet add package` line for it and any other item dependencies. Because the brain is not being vendored, `--styled-only` cannot be combined with `--namespace` (the namespace rewrite only makes sense when you own the brain source).

After copying, the command prints how to finish the wiring: add a `<PackageReference Include="Navius.Primitives" ... />`, register the brain services with `builder.Services.AddNavius()`, and (nothing to do for) the interop JavaScript, which ships inside the package as a static web asset and loads automatically from `_content/Navius.Primitives/navius-interop.js`.
