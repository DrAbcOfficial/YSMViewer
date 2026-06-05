# YSMViewer

A cross-platform 3D model viewer for `.ysm` files — Encrypted binary format used for Minecraft player models. Built with Avalonia and Aura3D.

## Quick Start

```powershell
git clone https://github.com/DrAbcOfficial/YSMViewer.git
cd YSMViewer
git submodule update --init --recursive
dotnet build YSMViewer.slnx
dotnet run --project YSMViewer.Desktop
```

Open a `.ysm` file via **Open YSM** button or launch with a file:

```powershell
dotnet run --project YSMViewer.Desktop -- path\to\model.ysm
```

- **SDK**: .NET 10.0
- **Submodule**: `YSMParser.Net/` ([YSMParser.NET](https://github.com/DrAbcOfficial/YSMParser.NET))

## Project Structure

| Project | Target | Purpose |
|---|---|---|
| `YSMViewer/` | `net10.0` | Shared UI library: Views, ViewModels, Services, 3D scene |
| `YSMViewer.Desktop/` | `net10.0` | Desktop launcher |
| `YSMViewer.Browser/` | `net10.0-browser` (WASM) | Browser launcher |
| `YSMParser.Net/` | submodule | Parser, CLI, GLB exporter (see its [README](YSMParser.Net/README.md)) |

## Tech Stack

- **UI**: [Avalonia](https://avaloniaui.net/) 12.0
- **3D**: [Aura3D](https://github.com/aarthificial/Aura3D)
- **MVVM**: [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (source generators)
- **Parser**: [YSMParser.Net](https://github.com/DrAbcOfficial/YSMParser.NET) (git submodule)

## Acknowledgements

- [YSMParser](https://github.com/OpenYSM/YSMParser) — original YSM format research
- [Aura3D](https://github.com/aarthificial/Aura3D) — 3D rendering engine
