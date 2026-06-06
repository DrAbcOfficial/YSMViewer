# YSMViewer

A cross-platform 3D model viewer for `.ysm` files — Encrypted binary format used for Minecraft player models. Built with Avalonia and Aura3D.

## Quick Start

```powershell
git clone https://github.com/DrAbcOfficial/YSMViewer.git
cd YSMViewer
dotnet build YSMViewer.slnx
dotnet run --project YSMViewer.Desktop
```

Open a `.ysm` file via **Open YSM** button or launch with a file:

```powershell
dotnet run --project YSMViewer.Desktop -- path\to\model.ysm
```

- **SDK**: .NET 10.0

## Live Preview

Try it in your browser: [YSMViewer Live](https://drabcofficial.github.io/YSMViewer/)

## Project Structure

| Project | Target | Purpose |
|---|---|---|
| `YSMViewer/` | `net10.0` | Shared UI library: Views, ViewModels, Services, 3D scene |
| `YSMViewer.Desktop/` | `net10.0` | Desktop launcher |
| `YSMViewer.Browser/` | `net10.0-browser` (WASM) | Browser launcher |

## Tech Stack

- **UI**: [Avalonia](https://avaloniaui.net/) 12.0
- **3D**: [Aura3D](https://github.com/cesun/Aura3D)
- **MVVM**: [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (source generators)
- **Parser**: [YSMParser.Core](https://www.nuget.org/packages/YSMParser.Core) (NuGet)

## Acknowledgements

- [YSMParser](https://github.com/OpenYSM/YSMParser) — original YSM format research
