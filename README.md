# YSMViewer

A cross-platform 3D model viewer for `.ysm` files — Encrypted binary format used for Minecraft player models. Built with Avalonia.

## Live Preview

Try it in your browser: [YSMViewer Live](https://drabcofficial.github.io/YSMViewer/)

## Quick Start

```bash
git clone https://github.com/DrAbcOfficial/YSMViewer.git
cd YSMViewer
dotnet build YSMViewer.slnx
dotnet run --project YSMViewer.Desktop
```

Open a `.ysm` file via **Open YSM** button or launch with a file:

```bash
dotnet run --project YSMViewer.Desktop -- path/to/model.ysm
```

- **SDK**: .NET 10.0

## Camera Controls

| Operation | Desktop | Browser |
|---|---|---|
| Orbit | Left mouse drag | Left mouse drag |
| Zoom | Mouse scroll / middle mouse drag | Mouse scroll / pinch |
| Pan | Right mouse drag | Right mouse drag |
| Reset view | Bottom bar **Reset** button | Bottom bar **Reset** button |
| Front view | Bottom bar **Front** button | Bottom bar **Front** button |
| Left view | Bottom bar **Left** button | Bottom bar **Left** button |
| Top view | Bottom bar **Top** button | Bottom bar **Top** button |

## Project Structure

| Project | Target | Purpose |
|---|---|---|
| `YSMViewer/` | `net10.0` | Shared UI library: Views, ViewModels, Services, 3D scene |
| `YSMViewer.Core/` | `net10.0` | Shared parsing/model library consumed by all other projects |
| `YSMViewer.Desktop/` | `net10.0` | Desktop launcher |
| `YSMViewer.Browser/` | `net10.0-browser` (WASM) | Browser launcher |
| `ThumbnailProviders/YSMViewer.ThumbnailProvider/` | `net10.0` | NativeAOT thumbnail rendering library |
| `ThumbnailProviders/YSMViewer.ThumbnailProvider.Win/` | Native (C++/COM) | Windows Explorer thumbnail provider |
| `ThumbnailProviders/YSMViewer.ThumbnailProvider.XDG/` | `net10.0` NativeAOT | Linux XDG thumbnailer CLI |
| `ThumbnailProviders/YSMViewer.ThumbnailProvider.OSX/` | Objective-C | macOS Quick Look thumbnail provider |

### Windows Thumbnail Provider

Build & register from a Visual Studio x64 Native Tools command prompt:

```powershell
# Build C# library
dotnet publish ThumbnailProviders/YSMViewer.ThumbnailProvider -c Release -r win-x64 -o publish/thumbnail-win

# Build native COM wrapper
msbuild ThumbnailProviders\YSMViewer.ThumbnailProvider.Win\YSMViewer.ThumbnailProvider.Win.vcxproj /p:Configuration=Release /p:OutDir="%cd%\publish\thumbnail-win\"

# Register (admin required)
.\publish\thumbnail-win\install.ps1

# Unregister
.\publish\thumbnail-win\uninstall.ps1
```

### Linux XDG Thumbnail Provider

The XDG thumbnailer uses MIME `application/vnd.ysm.model+encrypted` and covers file managers that honor freedesktop thumbnailers, such as Nautilus, Nemo, Caja, and Thunar. KDE Dolphin may require a future KIO plugin for first-class support.

```bash
dotnet publish ThumbnailProviders/YSMViewer.ThumbnailProvider -c Release -r linux-x64 -o publish/thumbnail-xdg
make -C ThumbnailProviders/YSMViewer.ThumbnailProvider.XDG BUILD_DIR="$PWD/publish/thumbnail-xdg"
cp ThumbnailProviders/YSMViewer.ThumbnailProvider.XDG/*.sh ThumbnailProviders/YSMViewer.ThumbnailProvider.XDG/*.in ThumbnailProviders/YSMViewer.ThumbnailProvider.XDG/*.xml publish/thumbnail-xdg/
cd publish/thumbnail-xdg
./install.sh
```

### macOS Thumbnail Provider

The macOS provider is a minimal Objective-C Quick Look generator that loads the NativeAOT rendering library from the generator bundle.

```bash
dotnet publish ThumbnailProviders/YSMViewer.ThumbnailProvider -c Release -r osx-arm64 -o publish/thumbnail-osx/native
make -C ThumbnailProviders/YSMViewer.ThumbnailProvider.OSX BUILD_DIR="$PWD/publish/thumbnail-osx" NATIVE_LIB="$PWD/publish/thumbnail-osx/native/libYSMViewer.ThumbnailProvider.dylib"
```

## Tech Stack

- **UI**: [Avalonia](https://avaloniaui.net/) 12.0
- **3D**: [Aura3D](https://github.com/cesun/Aura3D) (Desktop) / [Three.js](https://threejs.org/) (Browser via JS interop)
- **MVVM**: [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (source generators)
- **Parser**: [YSMParser.Core](https://www.nuget.org/packages/YSMParser.Core)
- **MoLang**: [Alex.MoLang](https://github.com/ConcreteMC/MolangSharp)

## Acknowledgements

- [YSMParser](https://github.com/OpenYSM/YSMParser) — original YSM format research
