# MenuBuilder (Editor/MenuBuilder.cs)

Builds the expressions-menu tree.

- `static VRCExpressionsMenu Build(WardrobeModel model, Action<Object> persist)`
  — returns a wrapper menu containing a single submenu entry (named
  `MenuName`); hand it to a menu installer. `persist` is called on every menu
  asset created.

Menu shape:

- Wardrobe menu starts with a None toggle (when enabled), then outfits in the
  "Default" category (case-insensitive) directly at the root, then one
  submenu per remaining category.
- An outfit with toggleable pieces becomes a submenu: a "Wear" toggle first,
  then one toggle per marked piece. An outfit without any stays a single
  toggle.
- Any menu exceeding `VRCExpressionsMenu.MAX_CONTROLS` is split into pages
  chained by a "Next" entry.
