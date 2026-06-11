# AGENTS.md — YSMViewer

## Prerequisites

- **.NET SDK 10.0** (`net10.0` target). No `global.json`; the SDK version is implied by the TFM.
- **Windows only** (ThumbnailProvider): Requires `net10.0-windows` + COM host support.

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

# Run thumbnail test harness
dotnet run --project YSMViewer.ThumbnailProvider -c DebugRuntest -- path\to\file.ysm [output.png] [size]
```

There are **no tests** in YSMViewer.

## Solution

- **Format**: `.slnx` (not `.sln`).
- **Central package management**: `Directory.Packages.props` — add new NuGet deps there, not in individual `.csproj` files.
- **Five projects**: `YSMViewer/` (shared UI), `YSMViewer.Core/` (shared parsing), `YSMViewer.Desktop/` (WinExe), `YSMViewer.Browser/` (WASM), `YSMViewer.ThumbnailProvider/` (COM thumbnail handler).

## CI (`.github/workflows/build.yml`)

- Every push builds `YSMViewer.Desktop -c Release` on ubuntu-latest.
- `v*` tag triggers full release: cross-platform desktop publish (win-x64, linux-x64, osx-arm64) + browser WASM publish + GitHub Pages deploy to `webpage` branch.
- Browser publish requires `dotnet workload install wasm-tools`.
- Desktop Release uses `PublishSingleFile`, `SelfContained`, `PublishTrimmed`.

## Architecture

### YSMViewer.Core

Shared model library (`net10.0`) consumed by all other projects. Contains:

- **Models/**: `YsmModelDocument`, `YsmGeometryModel`, `YsmTextureResource`, `YsmBoneInfo`, `YsmCubeInfo` — document model types.
- **Models/Document/**: `YsmModelDocument`, `MinecraftGeometry.cs`, `MinecraftAnimation.cs`, `MinecraftCubeFaceUV.cs`.
- **Services/**: `YsmLoaderService` (file → JSON → document), `YsmImageHelper` (PNG conversion via SixLabors.ImageSharp), `YsmMetadataParser`, `ZipYsmParser`.

NuGet: `YSMParser.Core`, `SixLabors.ImageSharp`.

### YSMViewer.ThumbnailProvider

Windows COM shell extension (`net10.0-windows`) that generates Explorer thumbnail previews for `.ysm` files using a CPU software renderer.

**Two configurations:**
- **Debug/Release**: `OutputType=Library`, `EnableComHosting=true` — produces `.comhost.dll` for COM registration.
- **DebugRuntest**: `OutputType=Exe`, `EnableComHosting=false` — standalone CLI test harness for local thumbnail testing.

**Key files:**
- `YsmThumbnailProvider.cs` — COM entry point, implements `IThumbnailProvider` + `IInitializeWithStream`.
- `ComInterfaces.cs` — COM interface definitions + `ComStreamWrapper`.
- `Rendering/SoftwareRenderer.cs` — CPU Z-buffer rasterizer with barycentric texture sampling and directional lighting.
- `Rendering/GeometryBuilder.cs` — bone hierarchy traversal, world-space quad generation from cube faces.
- `Test/TestHarness.cs` — CLI test harness with timing diagnostics.
- `Scripts/Register.ps1` — COM registration/unregistration (requires admin).

**NuGet (ThumbnailProvider only):** `System.Drawing.Common` (for `Bitmap.GetHbitmap()` COM interop), `SixLabors.ImageSharp` (via Core).

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

`AnimationService`, `LocalizationService`, `ThemeService`.

### Services (`YSMViewer.Core/Services/`)

`YsmLoaderService`, `YsmImageHelper`, `YsmMetadataParser`, `ZipYsmParser`.

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
