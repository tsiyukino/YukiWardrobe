# WardrobeWindow (Editor/WardrobeWindow.cs)

Dockable editing UI (`TsiYuki/Wardrobe Editor` menu item). A view over the
`YukiWardrobe` component: it stores no wardrobe data itself; every edit goes
to the component through `Undo`, followed by dirty/prefab-modification
marking.

- `static void ShowWindow()` — opens the window.
- `static void Open(YukiWardrobe config)` — opens it pointed at the config's
  avatar (used by the inspector button).

Features: avatar picker (auto-picks a lone scene avatar), one-click creation
of a "Wardrobe" child object when the avatar has none, "add selected as
outfits" with a category field, and a drag-reorderable outfit list — the top
row is the default (there is no separate default setting), each row expands
to per-piece checkboxes, a blendshape-override list (renderer, name popup
read from the mesh, 0–100 slider), and an object-override list (objects
outside the outfit with an On/Off button for their state while worn); the
built-in footer button removes the selected row. Validation warnings and parameter-bit usage come from
`WardrobeModel.Resolve`.

The list itself lives in `OutfitListGUI` (internal, one instance bound to one
component); `UndoEdit` (internal) wraps the record/dirty/prefab-modification
sequence both classes use.
