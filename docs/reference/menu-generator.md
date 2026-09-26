# MenuGenerator (Editor/MenuGenerator.cs)

Builds the wardrobe's menu as Modular Avatar menu items, with TsiYuki Core
Menus' `MenuItems`.

- `static GameObject Build(WardrobeModel model, Transform host)` — returns the
  menu's root under `host`. The root carries the installer; the caller places
  it (`MenuPlacement.Place`).

Shape:

- The root is a submenu named after the wardrobe, with its icon.
- Entries in list order. An entry with a category goes into a submenu for that
  category, created where the category's first entry is.
- An entry without anything to show inside is a single toggle setting
  `<param>` to its value.
- Any other entry is a submenu named after it, holding a **Wear** toggle
  (`<param>` = its value), a toggle per piece (the piece's bool), a **Colors**
  submenu with a toggle per color (the outfit's color int = its index) when it
  has colors, and — when its menus are set to **Absorb** — a Menu Install
  Target per menu shipped inside the outfit that can be absorbed. Without an
  install target in this Modular Avatar version, those menus are left where
  they are and a warning is logged.
- A **Looks** submenu with a button per look (`<param>/Look` = its value) when
  there are looks.

Every item has an explicit value; none uses Modular Avatar's automatic values.
The labels the wardrobe adds itself (**Wear**, **Colors**, **Looks**, **None**)
are always English, from `MenuText`: the menu is seen in game by everyone.
