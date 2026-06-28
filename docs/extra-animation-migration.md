# extra_animation Migration Notes

YSMViewer implements Fox-Model-Loader's `extra_animation` behavior as a static browser list instead of a radial roulette UI.

## Implemented

- `YSMViewer.Core/Models/Document/YsmModelDocument.cs`
  - `YsmExtraAnimationLayout`
  - `YsmExtraAnimationGroup`
  - `YsmExtraAnimationEntry`
- `YSMViewer.Core/Services/YsmLoaderService.cs`
  - Parses `ysm.json` `properties.extra_animation`.
  - Parses `properties.extra_animation_classify`.
  - Skips roulette navigation-only entries such as `#return` and `#group` links.
- `YSMViewer/ViewModels/MainViewModel.Animation.cs`
  - Prefers parsed extra animation groups in the animation panel.
- `YSMViewer.Desktop/Rendering/Aura3D/Aura3DRenderer.cs`
  - Injects selected animations into the controller state machine when controller mode is enabled.

## extra_animation_buttons Stub

Future `extra_animation_buttons` work should start from these stubs:

- `YSMViewer.Core/Models/Document/YsmModelDocument.cs`
  - `YsmExtraAnimationButtonDefinition` currently preserves `id`, `name`, and `description` only.
- `YSMViewer.Core/Services/YsmLoaderService.cs`
  - `ParseExtraAnimationButtonStubs` intentionally ignores config forms for now.

Expected next step: replace `YsmExtraAnimationButtonDefinition` with a full model for `config_forms`, then add a desktop UI panel that evaluates or mutates MoLang variables through `MolangService`.
