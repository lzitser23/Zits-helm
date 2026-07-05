<h1 align="center">zits/helm</h1>

<p align="center">
  <strong>The styled Blazor component layer for Navius: copy-paste source, Tailwind classes, and runtime theme tokens.</strong>
</p>

<p align="center">
  <a href="#overview">Overview</a> |
  <a href="#features">Features</a> |
  <a href="#installation">Installation</a> |
  <a href="#quick-start">Quick Start</a> |
  <a href="#stack">Stack</a> |
  <a href="#development">Development</a> |
  <a href="#architecture">Architecture</a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/license-MIT-171614" alt="MIT license" />
  <img src="https://img.shields.io/badge/.NET-8.0-171614" alt=".NET 8" />
  <img src="https://img.shields.io/badge/Blazor-Razor%20Class%20Library-737270" alt="Blazor Razor Class Library" />
  <img src="https://img.shields.io/badge/registry-82%20items-737270" alt="82 registry items" />
</p>

---

## Overview

**zits/helm** is the styled component layer for
[Navius](https://github.com/lzitser23/Navius). It ports the shadcn/ui component model
to Blazor as `Zits*` Razor components, built on top of the headless
`Navius.Primitives` brain and its Base UI-aligned behavior contract.

The repo has two distribution paths. `Zits.Ui` is the packable reference Razor Class
Library and static-asset package. The `navius` dotnet tool is the source-distribution
path: it copies registry items into an app so consumers own the component code.

---

## Features

- **Styled component catalog** — 337 `Zits*.razor` component parts live under
  `src/Zits.Ui/Components`, covering primitives, charts, chat components, forms,
  data display, overlays, navigation, pickers, and layout.
- **Headless behavior from Navius** — overlay, field, menu, select, carousel,
  drawer, data-grid, and other behavior delegates to `Navius.Primitives`; the helm
  owns classes, markup ergonomics, and theme tokens.
- **Self-contained registry CLI** — `tools/Navius.Cli` packs as the `navius` dotnet
  tool and bundles `registry/` plus `registry-source/`, so `navius add <item>` works
  without a local checkout after install.
- **82 registry items** — `registry/registry.json` includes `cn`, `core`, the styled
  component catalog, brain-vendoring primitives, `chat`, `button`, and the `theme`
  engine.
- **Runtime theming engine** — `ZitsThemeService`, `ZitsThemeSwitcher`,
  `ZitsThemeScope`, and generated `zits-theme.css` switch mode, gray scale, primary,
  radius, font, and style recipe through `data-zits-*` attributes.
- **Package-ready metadata** — `Zits.Ui` and `navius` are both locked to
  `0.3.0-preview.1` with MIT license metadata, package readmes, SourceLink, symbols,
  and repository URLs.

---

## Installation

Preview package IDs are prepared as `0.3.0-preview.1`. Until the packages are pushed
to NuGet, use the source-checkout workflow in [Development](#development). After
publish, install the styled layer and CLI with:

```bash
dotnet add package Zits.Ui --prerelease
dotnet tool install -g navius --prerelease
```

For repository development, keep the brain and helm repos checked out side by side:

```bash
git clone https://github.com/lzitser23/Navius.git navius
git clone https://github.com/lzitser23/Zits-helm.git zits-helm
```

```text
<parent>/
|-- navius/       # headless brain, engine, playground, e2e tests
`-- zits-helm/    # this repo: styled helm, CLI, registry
```

---

## Quick Start

1. Register Navius services and the helm services:

```csharp
builder.Services.AddNavius();
builder.Services.AddZitsUi();
```

2. Link the base zits/ui stylesheet in the host page:

```html
<link href="_content/Zits.Ui/zits-ui.css" rel="stylesheet" />
```

3. Add the theme registry item when you want runtime theme switching:

```bash
navius add theme --to <dir> --namespace <ns>
```

4. Include the no-flash theme init script and generated theme stylesheet before the
   app stylesheet:

```html
<script src="_content/Zits.Ui/zits-theme-init.js"></script>
<link href="_content/Zits.Ui/zits-theme.css" rel="stylesheet" />
```

---

## Stack

| Layer | Choice |
| --- | --- |
| Runtime | .NET 8 |
| Component package | `src/Zits.Ui` Razor Class Library |
| Brain dependency | Sibling `ProjectReference` to `../navius/src/Navius.Primitives` during repo development |
| CLI | `tools/Navius.Cli`, packed as the `navius` dotnet tool |
| Registry | `registry/registry.json`, 82 items |
| Styling | Tailwind utility classes and OKLCH tokens in `wwwroot/zits-ui.css` |
| Theming | `src/Zits.Ui/Theming` plus `src/Zits.Ui.CssGen` generated CSS |
| Tests | `tests/Zits.Ui.Tests` xUnit tests for theme CSS generation and contrast coverage |

---

## Development

### Prerequisites

- .NET 8 SDK.
- A sibling checkout of [Navius](https://github.com/lzitser23/Navius), because
  `src/Zits.Ui/Zits.Ui.csproj` references the brain by project path.

### Commands

```bash
git clone https://github.com/lzitser23/Navius.git navius
git clone https://github.com/lzitser23/Zits-helm.git zits-helm
cd zits-helm
```

```bash
# Build the styled component package
dotnet build src/Zits.Ui/Zits.Ui.csproj

# Build the registry CLI
dotnet build tools/Navius.Cli/Navius.Cli.csproj

# Run helm tests
dotnet test tests/Zits.Ui.Tests/Zits.Ui.Tests.csproj

# Pack preview artifacts locally
dotnet pack src/Zits.Ui/Zits.Ui.csproj -c Release
dotnet pack tools/Navius.Cli/Navius.Cli.csproj -c Release
```

The live showcase and Playwright e2e suite live in the `navius` repo's playground.
Run `dotnet run --project playground/Navius.Playground` from the sibling `navius`
checkout and use the `/ui` and `/fidelity` routes to exercise helm components.

### Project Structure

```text
zits-helm/
|-- registry/           # registry metadata and copy-paste item definitions
|-- src/Zits.Ui/        # styled Razor components, services, tokens, static assets
|-- src/Zits.Ui.CssGen/ # generator for committed theme CSS
|-- tests/              # xUnit tests for generated theme output
`-- tools/Navius.Cli/   # dotnet tool that lists and copies registry items
```

---

## Architecture

The helm is intentionally thin over the brain. Components such as `ZitsDialogContent`
compose Navius parts and apply class strings; the accessibility, focus, positioning,
field-state, and dismissal behavior stays in `Navius.Primitives`. Registry items point
at the source files users should own after copy-paste, while the packed CLI also
embeds those files under `registry-source/`.

- **[CONTEXT.md](CONTEXT.md)** — repo map, invariants, and release shape.
- **[registry/registry.json](registry/registry.json)** — the copy-paste registry.
- **[src/Zits.Ui/Theming](src/Zits.Ui/Theming)** — runtime theme model, service,
  scope, switcher, and stylesheet generation.

---

## Acknowledgments

- [Navius](https://github.com/lzitser23/Navius) for the headless primitive engine.
- [Base UI](https://base-ui.com) for the behavior contract Navius mirrors.
- [shadcn/ui](https://ui.shadcn.com) for the copy-paste component model.
- [Tailwind CSS](https://tailwindcss.com) for the utility-class styling model.

---

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).
