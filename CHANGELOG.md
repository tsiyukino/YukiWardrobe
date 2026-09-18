# Changelog

## [Unreleased] — 3.0.0

### Added
- **Absorbs outfit menus.** Modular Avatar menus that ship inside an outfit (for example a "Cloth Change"
  submenu) are moved into that outfit's wardrobe submenu instead of adding their own entry to the root
  menu. Per outfit: move (default), leave in place, or hide. Their parameters are untouched. The editor
  lists each outfit's menus and every item in them.
- **Several wardrobes per avatar** (for example Outfits and Hair). Each wardrobe is its own top-level menu,
  named after its object; parameter names are generated per wardrobe and never collide.
- **Custom display names and icons** for wardrobes, outfits, pieces, colors and looks. Renaming never
  changes parameter names, so saved selections survive.
- **Checks** shown in the editor and in NDMF's error window: objects or blendshapes that a wardrobe controls
  and that are also controlled by another wardrobe, an MA Object Toggle, an MA Shape Changer or an animator.
- **Whole-avatar parameter budget** bar, split by source.
- **Looks**: one-click outfit + piece combinations through a local-only parameter and parameter drivers.
- **Try on** in the editor through AnimationMode (nothing is written to the scene) and **menu icons**
  rendered from the avatar.
- **Outfit import**: drop a prefab to place it under the avatar, run MA Setup Outfit and add it; body
  blendshape suggestions for hiding skin under clothes.
- **Colors / material variants** per outfit, **PC-only / mobile-only** outfits, an optional **change
  effect** object, and **conversion of an outfit's simple MA toggles into wardrobe pieces**.
- New two-pane editor window with menu preview; English, Chinese and Japanese UI (TsiYuki > Language).
- NDMF parameter provider, so MA / NDMF tools see the wardrobe's parameters before the build.

### Changed
- The menu is generated as Modular Avatar Menu Items instead of menu assets (MA handles paging).
- All piece toggles share one Direct Blend Tree layer instead of one layer each.
- Warnings go to NDMF's error report.

### Changed data model (not compatible with 2.x)
- Entries have fixed parameter values: reordering the menu or changing the default no longer shifts saved
  selections. Value 0 means "the default entry", which is also what a switched-off toggle falls back to.
- The default entry is chosen explicitly instead of being the first in the list.
- Pieces, colors and wardrobes use generated ids in parameter names, so renaming objects or labels keeps saved values.
- Pieces are one list (target, name, icon); "None" is a normal entry that can sit anywhere in the menu.
- Colors are a table: the slots that change, and one material per slot for each color.
- One display name for the wardrobe menu (empty = object name).

## [2.0.0] - 2026-07-15

- Rewritten as a `YukiWardrobe` component plus an NDMF plugin that emits Modular Avatar
  components at build time (see `docs/decisions/2026-07-15_component-ndmf-rewrite.md`).
- Per-piece toggles, blendshape overrides, object overrides, categories, optional None.
