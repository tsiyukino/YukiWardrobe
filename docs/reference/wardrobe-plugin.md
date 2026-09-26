# WardrobePlugin (Editor/WardrobePlugin.cs)

NDMF plugin `moe.tsiyuki.wardrobe`, one pass ("Generate wardrobe") in the
Generating phase, ordered before TsiYuki Core Menus (`.BeforePlugin<MenusPlugin>()`, which settles menus
installed into other TsiYuki menus) and before Modular Avatar.

For an avatar with at least one `YukiWardrobe`:

1. `EnsureIds()` on every component (build copies may never have been
   validated in the editor), then `WardrobeSet.Resolve` over the avatar.
2. Every model's warnings go to NDMF as non-fatal errors, and every
   `ConflictChecker` finding as information, through `WardrobeText.Errors`
   (a TsiYuki Core `YukiNdmfReport`).
3. Per model: the installers of outfit menus set to **Hide** are removed;
   when the model has entries, the wardrobe is generated (below); outfits
   excluded for the platform being built are destroyed.
4. Every `YukiWardrobe` component is destroyed.

Generating one wardrobe puts everything on a new object,
`<menu name> (Yuki Wardrobe)`, under the avatar root:

- `ModularAvatarMergeAnimator` — the controller from `AnimatorBuilder.Build`,
  FX layer, absolute paths, write defaults left as built (off).
- `ModularAvatarParameters` — from `BuildParameters`:
  - `<param>`: int, default 0, saved per the component;
  - one bool per piece, default from `DefaultOn`, saved per the component;
  - one int per outfit with colors (`<param>/<entryId>/Color`), default 0;
  - `<param>/Look`: int, local only, not saved (only when there are looks);
  - `TsiYuki/Wardrobe/One`: float 1, not synced (only when there are pieces;
    the Direct blend tree's weight).
- The menu from `MenuGenerator.Build`, placed with TsiYuki Core Menus'
  `MenuPlacement.Place` where the component's **Install into** points (the
  avatar's root menu when empty).

Generated assets are saved through `BuildContext.AssetSaver`.

`WardrobeParameterProvider` reports the same synced parameters to NDMF and
Modular Avatar tools (parameter usage, budget displays) before a build.
