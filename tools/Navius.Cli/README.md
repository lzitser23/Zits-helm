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
