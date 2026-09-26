# Architecture (3.1)

Data lives only in `YukiWardrobe` components (Runtime). Everything else is editor code.

| File | Role |
| --- | --- |
| `Runtime/YukiWardrobe.cs` | Serialized configuration. 2.x fields are kept, new ones have safe defaults. |
| `Editor/WardrobeModel.cs` | `WardrobeSet` resolves every wardrobe of an avatar together (unique parameter names), `WardrobeModel` is one resolved wardrobe: outfits, pieces, blendshape/object channels, variants, looks, warnings. Index 0 is the default outfit. |
| `Editor/OutfitMenuScanner.cs` | Finds the MA menus (and VRCFury components) shipped inside an outfit and describes them as a `MenuNode` tree. |
| `Editor/AnimatorBuilder.cs` | FX controller: outfit layer (int), one Direct Blend Tree layer for all pieces, one layer per outfit with variants, a local Looks layer with parameter drivers. WD off; every state writes every property of its layer. |
| `Editor/MenuGenerator.cs` | MA Menu Item tree; absorbed outfit menus are attached with MA Menu Install Target. |
| `Editor/WardrobePlugin.cs` | NDMF plugin (Generating, before MA): reports warnings/conflicts to NDMF, hides menus set to Hide, removes platform-excluded outfits, emits Merge Animator + Parameters + menu, removes the configs. Also the NDMF parameter provider. |
| `Editor/ConflictChecker.cs` | Objects / blendshapes also controlled by other wardrobes, MA Object Toggle, MA Shape Changer or animators. |
| `Editor/WardrobeWindow.cs` | Two-pane editor. Holds no data; all edits go through Undo. |
| `Editor/YukiWardrobeEditor.cs` | The component's inspector: a summary and a button to the window, which survives Hierarchy selection changes. |
| `Editor/WardrobeMenu.cs` | Hierarchy right-click **GameObject > TsiYuki > Add to Wardrobe**, applied once to the whole selection. |
| `Editor/WardrobeText.cs` | UI strings (`Localization/<code>.txt`) and `WardrobeText.Errors` for NDMF; `MenuText`, the always-English labels the menu adds. |
| `Editor/WardrobePreview.cs` | Try-on through AnimationMode property modifications (reverted on stop, never saved). |
| `Editor/WardrobeIcons.cs` | Renders outfit icons from a posed copy in a preview scene. |
| `Editor/WardrobeActions.cs` | Outfit import (MA Setup Outfit), shrink-blendshape suggestions, toggle-to-piece conversion, parameter budget. |

Code shared with the other TsiYuki tools comes from the TsiYuki Core packages, and the dependencies point
only that way:

- TsiYuki Core (`TsiYuki.Core.Editor`): localization, the common GUI, `UndoEdit` and `YukiNdmfReport` (build
  warnings in NDMF's error window, from the same localization tables).
- TsiYuki Core Animation (`TsiYuki.Core.Animations.Editor`): `AnimatorGraph`, the layers, states and entry
  transitions `AnimatorBuilder` assembles.
- TsiYuki Core Menus (`TsiYuki.Core.Menus.Editor`): `MenuItems` and `InstallTarget`, which `MenuGenerator`
  builds with, and `MenuPlacement`, which installs the finished menu where **Install into** points. The
  plugin's pass runs before `moe.tsiyuki.core.menus`, which settles moves into another TsiYuki menu.

See [decisions/2026-09-26_share-by-field.md](../decisions/2026-09-26_share-by-field.md).

Parameters: `<param>` (int; each entry has a fixed value, 0 = default entry), `<param>/<pieceId>` (bool),
`<param>/<entryId>/Color` (int), `<param>/Look` (int, local only). `<param>` is `Wardrobe/<wardrobe id>` unless
set explicitly. Ids and values are assigned by `YukiWardrobe.EnsureIds()` and never derived from names or order.
