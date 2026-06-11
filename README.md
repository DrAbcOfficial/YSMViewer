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
| `YSMViewer.ThumbnailProvider/` | `net10.0-windows` | Windows Explorer thumbnail handler for `.ysm` files |
| `YSMViewer.ThumbnailProvider.Cpp/` | Native (C++/COM) | Native COM thumbnail provider shell extension (replaces .NET version in-place) |

### Windows Thumbnail Provider

Two implementations are available:

- **C#** (`YSMViewer.ThumbnailProvider`): .NET-based COM shell extension using `EnableComHosting`.
- **C++** (`YSMViewer.ThumbnailProvider.Cpp`): Native COM DLL with lower overhead — recommended for production use.

Build & register the C++ version:

```powershell
# Build (run from Visual Studio x64 Native Tools command prompt)
.\YSMViewer.ThumbnailProvider.Cpp\build.bat

# Register (admin required)
.\YSMViewer.ThumbnailProvider.Cpp\install.ps1

# Unregister
.\YSMViewer.ThumbnailProvider.Cpp\uninstall.ps1
```

Register the C# version:

```powershell
# Register (admin required)
.\YSMViewer.ThumbnailProvider\Scripts\Register.ps1 -Register

# Unregister
.\YSMViewer.ThumbnailProvider\Scripts\Register.ps1 -Unregister
```

## Tech Stack

- **UI**: [Avalonia](https://avaloniaui.net/) 12.0
- **3D**: [Aura3D](https://github.com/cesun/Aura3D) (Desktop) / [Three.js](https://threejs.org/) (Browser via JS interop)
- **MVVM**: [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (source generators)
- **Parser**: [YSMParser.Core](https://www.nuget.org/packages/YSMParser.Core)
- **MoLang**: [Alex.MoLang](https://github.com/ConcreteMC/MolangSharp)

## Acknowledgements

- [YSMParser](https://github.com/OpenYSM/YSMParser) — original YSM format research
