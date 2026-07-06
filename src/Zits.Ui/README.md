# Zits.Ui

Styled Blazor components, theme tokens, and runtime theming built on `Navius.Primitives`.

## Install

```bash
dotnet add package Zits.Ui --prerelease
```

Register the services once in your app:

```csharp
builder.Services.AddNavius();
builder.Services.AddZitsUi();
```

Reference the package styles and theme initialization assets from your app shell:

```html
<script src="_content/Zits.Ui/zits-theme-init.js"></script>
<link href="_content/Zits.Ui/zits-ui.css" rel="stylesheet" />
<link href="_content/Zits.Ui/zits-theme.css" rel="stylesheet" />
```

`Zits.Ui` is also distributed as source through the `navius` CLI registry for projects that want direct ownership of component code.
