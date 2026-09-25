# WardrobePlugin (Editor/WardrobePlugin.cs)

NDMF plugin (`moe.tsiyuki.wardrobe`), one pass in the Generating phase, which
runs before Modular Avatar's Transforming-phase passes.

For each `YukiWardrobe` under the avatar root: resolve the model, report its
warnings to NDMF (through `WardrobeText.Errors`, a TsiYuki Core `YukiNdmfReport`), and — when at least one valid outfit exists — attach to the
component's GameObject:

- `ModularAvatarMergeAnimator` — the built FX controller, FX layer, absolute
  path mode.
- `ModularAvatarParameters` — the int (default 0, i.e. the fallback outfit)
  plus one bool per piece toggle, saved per the component setting.
- `ModularAvatarMenuInstaller` — the built menu, installed to the avatar's
  root menu.

The `YukiWardrobe` component is destroyed afterwards either way. Generated
objects are persisted through `BuildContext.AssetSaver`. Multiple wardrobe
components on one avatar log a warning about parameter-name collisions.
