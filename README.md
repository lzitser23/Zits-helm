# zits/helm

The styled **helm** layer for [Navius](../navius): shadcn/ui's component code ported to
Blazor as `Zits*` components, built on top of the Navius headless **brain**
(`Navius.Primitives`, aligned 1:1 with the [Base UI](https://base-ui.com) contract).
The helm keeps shadcn-style part names (`ZitsDialogContent` wraps the brain's
`Positioner` + `Popup`, etc.) and themes with Tailwind + an OKLCH token palette.

## Layout

```
src/Zits.Ui/        the styled components (Razor Class Library) — the registry source
tools/Navius.Cli/   the `navius` CLI (dotnet tool): list/add registry items
registry/           registry.json distribution metadata + copy-paste lib (Cn.cs)
```

## The sibling checkout

The brain is consumed by **path reference into the sibling `navius` repo** until it
ships on NuGet — check both repos out side by side:

```
<parent>/
  navius/       the brain (headless primitives + JS engine)
  zits-helm/    this repo
```

Related repos, same convention: `navius-docs` (brain docs site) and `zits-ui`
(helm docs site — the ui.shadcn.com-style showcase for these components).

## Build

```bash
dotnet build src/Zits.Ui/Zits.Ui.csproj
dotnet build tools/Navius.Cli/Navius.Cli.csproj
```

The component showcase and Playwright e2e suite live in the `navius` repo's
playground (`dotnet run --project playground/Navius.Playground` there — the `/ui`
and `/fidelity` routes exercise the helm).

## The registry

`registry/registry.json` follows the `registry-item` schema. `core`/`dialog`/`popover`
items vendor brain source via sibling-relative paths (`../navius/src/...`); run the CLI
from this repo's root:

```bash
dotnet run --project tools/Navius.Cli -- list
dotnet run --project tools/Navius.Cli -- add dialog --to <dir> --namespace <ns>
```

Wiring the full helm component set into the registry is tracked, not yet done.

## License

MIT — see [LICENSE](LICENSE).
