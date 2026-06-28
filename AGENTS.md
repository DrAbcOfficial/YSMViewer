# AGENTS.md — YSMViewer

## Prerequisites

- **.NET SDK 10.0** (`net10.0` target). No `global.json`; the SDK version is implied by the TFM.
- **Thumbnail providers**: Windows wrapper requires MSVC/Windows SDK; macOS wrapper builds on macOS; XDG thumbnailer targets Linux.

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

# Publish thumbnail rendering library
dotnet publish ThumbnailProviders/YSMViewer.ThumbnailProvider -c Release -r win-x64
```

There are **no tests** in YSMViewer.

## Solution

- **Format**: `.slnx` (not `.sln`).
- **Central package management**: `Directory.Packages.props` — add new NuGet deps there, not in individual `.csproj` files.
- **Core app projects**: `YSMViewer/` (shared UI), `YSMViewer.Core/` (shared parsing), `YSMViewer.Desktop/` (WinExe), `YSMViewer.Browser/` (WASM).
- **Thumbnail providers** live under `ThumbnailProviders/`: shared NativeAOT renderer, Windows COM wrapper, Linux XDG thumbnailer, macOS Quick Look wrapper.

## CI (`.github/workflows/build.yml`)

- Every push builds `YSMViewer.Desktop -c Release` on ubuntu-latest.
- `v*` tag triggers full release: cross-platform desktop publish (win-x64, linux-x64, osx-arm64) + parallel thumbnail provider publish (win-x64, linux-x64 XDG, osx-arm64) + browser WASM publish + GitHub Pages deploy to `webpage` branch.
- Browser publish requires `dotnet workload install wasm-tools`.
- Desktop Release uses `PublishSingleFile`, `SelfContained`, `PublishTrimmed`.

## Architecture

### YSMViewer.Core

Shared model library (`net10.0`) consumed by all other projects. Contains:

- **Models/**: `YsmModelDocument`, `YsmGeometryModel`, `YsmTextureResource`, `YsmBoneInfo`, `YsmCubeInfo` — document model types.
- **Models/Document/**: `YsmModelDocument`, `MinecraftGeometry.cs`, `MinecraftAnimation.cs`, `MinecraftCubeFaceUV.cs`.
- **Services/**: `YsmLoaderService` (file → JSON → document), `YsmImageHelper` (PNG conversion via SixLabors.ImageSharp), `YsmMetadataParser`, `ZipYsmParser`.

NuGet: `YSMParser.Core`, `SixLabors.ImageSharp`.

### ThumbnailProviders/YSMViewer.ThumbnailProvider

Cross-platform NativeAOT rendering library (`net10.0`) that generates `.ysm` thumbnail pixels using a CPU software renderer. Platform wrappers load this library and call exported C ABI functions.

**Exports:**
- `YsmThumbnail_Create`
- `YsmThumbnail_Render`
- `YsmThumbnail_Destroy`

**Key files:**
- `NativeAotCom/NativeExports.cs` — C ABI exported functions.
- `Rendering/SoftwareRenderer.cs` — CPU Z-buffer rasterizer with barycentric texture sampling and directional lighting.
- `Rendering/GeometryBuilder.cs` — bone hierarchy traversal, world-space quad generation from cube faces.

**NuGet:** `SixLabors.ImageSharp`.

### ThumbnailProviders/YSMViewer.ThumbnailProvider.Win

Native C++ COM shell extension for Windows Explorer. It co-locates `YSMViewer.ThumbnailProvider.dll` and loads it with `LoadLibraryW`.

**Build:** Requires Visual Studio x64 Native Tools command prompt. CI builds the `.vcxproj` with MSBuild after publishing the shared NativeAOT renderer.

**Key files:**
- `YsmThumbnailProvider.cpp` / `.h` — COM class factory + `IThumbnailProvider`/`IInitializeWithStream` implementation.
- `DllMain.cpp` — DLL entry point.
- `YsmThumbnailProvider.def` — module-definition file exporting `DllGetClassObject`, `DllCanUnloadNow`, `DllRegisterServer`, `DllUnregisterServer`.
- `install.ps1` / `uninstall.ps1` — COM registration helpers.

**Dependencies:** `shlwapi.lib`, `gdi32.lib`, `ole32.lib`, `advapi32.lib`, `user32.lib` — all Windows SDK, no MSVC redistributable required.

### ThumbnailProviders/YSMViewer.ThumbnailProvider.XDG

Linux XDG thumbnailer CLI. It is a small C/Makefile project that loads `libYSMViewer.ThumbnailProvider.so` with `dlopen`, calls the C ABI renderer, and writes PNG thumbnails for `.thumbnailer` consumers.

**Key files:**
- `ysm-thumbnailer.c` — CLI entry point: `ysm-thumbnailer INPUT OUTPUT [SIZE]`.
- `Makefile` — builds the CLI.
- `ysm.thumbnailer.in` — freedesktop thumbnailer template using MIME `application/vnd.ysm.model+encrypted`.
- `application-vnd-ysm-model-encrypted.xml` — shared MIME info for `.ysm`.
- `install.sh` / `uninstall.sh` — user-level install helpers.

KDE Dolphin may require a future KIO plugin for first-class support; none is implemented currently.

### ThumbnailProviders/YSMViewer.ThumbnailProvider.OSX

Minimal Objective-C Quick Look generator. It loads `libYSMViewer.ThumbnailProvider.dylib` from the `.qlgenerator` bundle and calls the C ABI renderer.

**Key files:**
- `GenerateThumbnailForURL.m` — Quick Look generator entry points.
- `Info.plist` — bundle metadata and supported types.
- `Makefile` — builds `YSMViewerThumbnailProvider.qlgenerator`.
- `install.sh` / `uninstall.sh` — user-level Quick Look install helpers.

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
- The C++ project requires a Visual Studio x64 Native Tools command prompt; it does not participate in `dotnet build`.
- `ViewLocator` has `[RequiresUnreferencedCode]` — trimming is aware of this.
- `WasmBuildNative` is enabled for the Browser project.
- No `Directory.Build.props` exists.
- No `opencode.json` — no repo-local OpenCode config.
