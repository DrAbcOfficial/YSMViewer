# AGENTS.md — YSMViewer

## Prerequisites

- **.NET SDK 10.0** (`net10.0` target). No `global.json`; the SDK version is implied by the TFM.

## NuGet

`YSMParser.Core` is consumed from **nuget.org** (no submodule, no local project reference).

## Build & Run

```powershell
# Build everything
dotnet build YSMViewer.slnx

# Run desktop
dotnet run --project YSMViewer.Desktop

# Open a file on launch
dotnet run --project YSMViewer.Desktop -- path\to\file.ysm
```

There are **no tests** in YSMViewer.

## Solution

- **Format**: `.slnx` (not `.sln`).
- **Central package management**: `Directory.Packages.props` — add new NuGet deps there, not in individual `.csproj` files.
- **Three projects**: `YSMViewer/` (shared lib), `YSMViewer.Desktop/` (WinExe entrypoint), `YSMViewer.Browser/` (WASM entrypoint).

## CI (`.github/workflows/build.yml`)

- Every push builds `YSMViewer.Desktop -c Release` on ubuntu-latest.
- `v*` tag triggers full release: cross-platform desktop publish (win-x64, linux-x64, osx-arm64) + browser WASM publish + GitHub Pages deploy to `webpage` branch.
- Browser publish requires `dotnet workload install wasm-tools`.
- Desktop Release uses `PublishSingleFile`, `SelfContained`, `PublishTrimmed`.

## Architecture

### Two rendering backends

An `IRenderer` abstraction (`Rendering/IRenderer.cs`) has two implementations:
- **Desktop** (`Aura3D/Aura3DRenderer.cs`) — Aura3D + GLTF loader.
- **Browser** (`ThreeJs/ThreeJsRenderer.cs`) — Three.js via JS interop (no Aura3D in WASM).

`App.axaml.cs` selects the renderer based on `ApplicationLifetime` type.

### Entrypoints

- **Desktop** (`YSMViewer.Desktop/Program.cs`): `[STAThread] Main` sets `App.StartupFilePath` from `args[0]`, calls `StartWithClassicDesktopLifetime`.
- **Browser** (`YSMViewer.Browser/Program.cs`): reads `?file=` from query string → `App.StartupFileUrl`, calls `StartBrowserAppAsync`.

### MVVM

- **CommunityToolkit.Mvvm** source generators (`[ObservableProperty]`, `[RelayCommand]`).
- `ViewLocator` resolves `FooViewModel` → `FooView` by string convention (reflection; trimming suppressed via `UnconditionalSuppressMessage`).
- `MainViewModel` is split across partial files: `MainViewModel.cs`, `MainViewModel.Animation.cs`, `MainViewModel.BoneTree.cs`, `MainViewModel.NestedViewModels.cs`.
- Compiled bindings are enabled by default (`AvaloniaUseCompiledBindingsByDefault=true`); use `x:DataType` on elements.

### Services (`YSMViewer/Services/`)

`YsmLoaderService`, `MeshBuilderService`, `AnimationService`, `LocalizationService`, `ThemeService`, `YsmImageHelper`, `YsmMetadataParser`, `ZipYsmParser`.

### Views (`YSMViewer/Views/`)

- `MainWindow` — desktop shell
- `BrowserMainView` — browser shell
- `MainView` — shared 3D scene view
- `FolderBrowserView` — folder navigation
- `Shared/ModelToolBar`, `Shared/ModelBottomBar` — reusable components
- `SphericalGizmo.cs` — custom control (code-only, no .axaml)

### Assets

`Assets/` under `YSMViewer/` is embedded as `<AvaloniaResource>`. Browser also has its own `Assets/` with CJK/emoji fonts (`NotoSansSC`, `NotoSansKR`, `NotoSansJP`, `NotoColorEmoji`).

## Conventions & quirks

- `AllowUnsafeBlocks` is set in both `YSMViewer.csproj` and `YSMViewer.Browser.csproj`.
- `AvaloniaUI.DiagnosticsSupport` (developer tools) is Debug-only, excluded in Release via `IncludeAssets`/`PrivateAssets` conditions.
- `ViewLocator` has `[RequiresUnreferencedCode]` — trimming is aware of this.
- `WasmBuildNative` is enabled for the Browser project.
- No `Directory.Build.props` exists.
- No `opencode.json` — no repo-local OpenCode config.
