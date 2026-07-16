# CONTEXT - zits/helm

Domain map and conventions for the styled helm repository. Read this before changing
components, registry items, package metadata, or CI.

## What this is

`zits/helm` is the styled component layer for Navius:

- `src/Zits.Ui` - the Razor Class Library with styled `Zits*` wrappers, theme tokens,
  runtime theming, and static web assets.
- `tools/Navius.Cli` - the `navius` dotnet tool that copies registry source into a
  consumer app.
- `registry/` - registry metadata and copy-paste source mapping.
- `tests/Zits.Ui.Tests` - generator, theme, contrast, and package-adjacent unit tests.

The helm depends on the sibling brain repo during local development:
`..\..\..\navius\src\Navius.Primitives\Navius.Primitives.csproj`. CI checks out
`lzitser23/Navius` side by side and builds from that shape.

## OSS boundary

The public technology repos are:

- `https://github.com/lzitser23/Navius`
- `https://github.com/lzitser23/Zits-helm`

The `navius-docs` and `zits-ui` docs/showcase repos are private sibling repos. Do not
present them as public GitHub surfaces in package metadata, public READMEs, or registry
homepage fields.

## Packaging

Preview package IDs are locked at `0.3.0-preview.2`:

- `Zits.Ui` - compiled styled component and static-asset package.
- `navius` - self-contained dotnet tool package.

Package metadata includes MIT license expression, package readmes, canonical repository
URLs, SourceLink, `.snupkg` symbols, and package tags. `Zits.Ui` still uses a sibling
`ProjectReference` locally, but pack output resolves Navius as a package dependency.

The `navius` CLI bundles `registry/` plus `registry-source/`, so `navius list` and
`navius add <item>` work after tool install without a repo checkout. Local development
can still override with `--root` and `--registry`.

## Registry and theming

`registry/registry.json` is source-of-truth metadata. Paths are repo-relative in the
working tree and bundled into the CLI package at pack time.

The `theme` registry item ships the generated token engine:

- `ThemeStylesheet.Generate()` mirrors the Navius Motion CSS generator pattern:
  deterministic C# output, generated `wwwroot/zits-theme.css`, and drift tests.
- `ZitsThemeService` persists the active theme and mirrors it to the DOM.
- `ZitsThemeSwitcher` is the styled customizer.
- `ZitsThemeScope` applies scoped theme attributes to a subtree.
- `zits-theme-init.js` restores theme choice before first paint and re-applies it
  after Blazor enhanced navigation, which otherwise resets `<html>` to the
  server-rendered markup and discards the client-applied theme.

Base tokens live in `src/Zits.Ui/wwwroot/zits-ui.css` as OKLCH `:root` and `.dark`
blocks plus Tailwind v4 `@theme inline` mapping. Components consume tokens through
utilities, not hard-coded color values.

## Build and test

Use these from the repo root:

```bash
dotnet build src/Zits.Ui/Zits.Ui.csproj
dotnet build tools/Navius.Cli/Navius.Cli.csproj
dotnet test tests/Zits.Ui.Tests/Zits.Ui.Tests.csproj
dotnet pack src/Zits.Ui/Zits.Ui.csproj -c Release
dotnet pack tools/Navius.Cli/Navius.Cli.csproj -c Release
```

The browser showcase and Playwright e2e suite live in the sibling `Navius` repo's
playground (`/ui` and `/fidelity` routes exercise this helm).

## OSS readiness

2026-07-05 `oss-ready` scanner pass on `E:\Lzitser\zits-helm` found no
high-confidence working-tree secrets, no history secrets, no tracked sensitive/junk
filenames, and no tracked files over 5 MB. Workflows reference no repo secrets or
variables beyond automatic `GITHUB_TOKEN`, so there are no required secrets to add
before CI.

Expected scanner warnings are public owner/license/repository identity strings. Do not
run fresh-history migration, push to a new public remote, or flip visibility without an
explicit target `OWNER/REPO` and explicit user confirmation.
