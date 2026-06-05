# AGENTS.md — YSMViewer

## Prerequisites

- **.NET SDK 10.0** (`net10.0` target). Install the SDK, then build. No `global.json` root pin; the SDK version is implied by the TFM.

## Submodule

~~`YSMParser.Net/` is a **git submodule** (not a plain copy). After a fresh clone:~~

~~```powershell
git submodule update --init --recursive
```~~

~~The submodule points to `https://github.com/DrAbcOfficial/YSMParser.NET`. See `YSMParser.Net/AGENTS.md` for its build/test/architecture details.~~

YSMParser.Core is consumed as a **NuGet package** (`YSMParser.Core` 1.0.0) from nuget.org. No submodule needed.

## Build & Run

```powershell
# Build everything (desktop + browser)
dotnet build YSMViewer.slnx

# Run the desktop app
dotnet run --project YSMViewer.Desktop

# Run the desktop app, opening a file on launch
dotnet run --project YSMViewer.Desktop -- path\to\file.ysm
```

There are **no tests** in YSMViewer itself.

## Solution & Toolchain

- **Solution format**: `.slnx` (new XML format, not legacy `.sln`).
- **Central package management**: `Directory.Packages.props` pins all NuGet versions.
- **Targets**: Desktop (`net10.0`, WinExe) and Browser (`net10.0-browser`, WASM via `Microsoft.NET.Sdk.WebAssembly`).
- **CI**: `.github/workflows/build.yml` — every push builds + uploads artifact; `v*` tag creates release + deploys Browser to `webpage` branch for GitHub Pages.

## Project Map

| Project | Type | Purpose |
|---|---|---|
| `YSMViewer/` | Library | Main UI. Avalonia views, ViewModels (CommunityToolkit.Mvvm), services, 3D viewport (Aura3D). |
| `YSMViewer.Desktop/` | Exe (WinExe) | Desktop launcher. `Program.cs` handles `STAThread`, optional file-open arg, developer tools in Debug. |
| `YSMViewer.Browser/` | Exe (WASM) | Browser/WASM launcher. Single-view lifetime, `AllowUnsafeBlocks`. |

Only the Desktop and Browser projects are entrypoints. `YSMViewer/` is just the shared UI library.

## Architecture Notes

### MVVM pattern

Uses **CommunityToolkit.Mvvm** (source generators). ViewModels use `[ObservableProperty]`, `[RelayCommand]`, etc. The `ViewLocator` resolves Views from ViewModels by convention:

```
YSMViewer.ViewModels.FooViewModel -> YSMViewer.Views.FooView
```

`ViewModelBase` extends `ObservableObject`. Do not expect XAML code-behind for logic — use commands and bindings.

### Avalonia specifics

- **Compiled bindings enabled by default** (`AvaloniaUseCompiledBindingsByDefault=true`). Use `x:DataType` on elements.
- **3D rendering** uses **Aura3D.Avalonia** (0.0.3) + `Aura3D.Model.GltfLoader` for GLB scene loading.
- Desktop uses `IClassicDesktopStyleApplicationLifetime`; Browser uses `ISingleViewApplicationLifetime`. The `App` class checks both in `OnFrameworkInitializationCompleted`.
- `App.StartupFilePath` is set from `args[0]` by `Program.cs` before the Avalonia app starts. The `MainViewModel` picks it up to auto-load a model.

### Views / components

Key view files (under `YSMViewer/Views/`):
- `MainWindow.axaml` / `MainView.axaml` — desktop vs browser shells
- `SphericalGizmo.cs` — custom control (code-only, no .axaml)
- `RadialMenu.cs` — custom control (code-only, no .axaml)

### Services

Under `YSMViewer/Services/`:
- `YsmLoaderService.cs` — wraps YSMParser.Core to load .ysm files
- `MeshBuilderService.cs` — builds Aura3D meshes from parsed geometry
- `AnimationService.cs` — handles animation playback

### Avalonia resources

`YSMViewer/Assets/` is included as `<AvaloniaResource>`. All files under it are embedded as Avalonia resources, not as plain content files.
