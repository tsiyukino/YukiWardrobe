# WardrobeWindow (Editor/WardrobeWindow.cs)

Dockable editing window (**TsiYuki > Wardrobe Editor**) over the `YukiWardrobe` components of one avatar. It
holds no wardrobe data: every edit goes straight to a component through `UndoEdit` (TsiYuki Core), so the window
can be closed at any time without losing anything.

- `static void ShowWindow()` — opens the window.
- `static void Open(YukiWardrobe config)` — opens it on that component (used by the inspector button).

Layout:

- **Top:** one tab per wardrobe on the avatar, and a button to add another.
- **Left:** **Wardrobe settings**, the list of entries (outfits, and None when enabled) in menu order,
  reorderable by dragging, and a drop area that adds objects as outfits. The avatar's parameter budget is shown
  underneath.
- **Right, wardrobe settings:** object name, menu name, icon, parameter name (with the name that will be used
  when left empty), **Saved**, **Install into** (TsiYuki Core Menus' `MenuParentField`; empty is the avatar's
  root menu), the default entry, the change effect object and its duration, and a summary of outfits, pieces,
  looks and parameter bits.
- **Right, an entry:** try-on preview, icon rendering, making it the default and removing it, then tabs for its
  general settings, pieces, blendshape overrides, object overrides and the menus shipped inside the outfit.

Validation warnings, conflicts and parameter usage come from `WardrobeModel`/`WardrobeSet` and
`ConflictChecker`.
