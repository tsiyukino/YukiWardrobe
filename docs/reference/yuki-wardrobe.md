# YukiWardrobe (Runtime/YukiWardrobe.cs)

`TsiYuki.Wardrobe.YukiWardrobe : MonoBehaviour, IEditorOnly` — configuration
component, one per avatar, anywhere inside the avatar hierarchy. Pure data;
consumed and removed at build time.

- `List<OutfitGroup> outfits` — the wardrobe. Order matters: the first outfit
  is the spawn default and the toggle-off fallback.
- `bool includeNone` — adds a "None" (nothing worn) menu option, default true.
- `string parameterName` — synced int parameter name, default `WardrobeState`.
- `string menuName` — label of the expressions-menu entry, default `Wardrobe`.
- `bool saved` — whether parameters persist across worlds, default true.

`TsiYuki.Wardrobe.OutfitGroup` (serializable):

- `GameObject root` — parent whose direct children are the outfit's pieces.
- `string category` — menu grouping label, default `Default`. Outfits in the
  `Default` category sit at the wardrobe menu root; every other category gets
  its own submenu.
- `List<GameObject> toggleablePieces` — the direct children of `root` chosen
  to get their own synced bool and menu toggle; empty means the outfit is a
  single on/off unit.
- `List<BlendshapeOverride> blendshapes` — avatar blendshapes to set while
  this outfit is worn.
- `List<ObjectOverride> objectOverrides` — objects elsewhere in the avatar
  forced on or off while this outfit is worn, e.g. underwear an outfit needs
  (on) or a bandage a tight outfit clips through (off). The same object may
  be overridden by several outfits.

`TsiYuki.Wardrobe.ObjectOverride` (serializable):

- `GameObject target` — object inside the avatar but outside any outfit root.
- `bool enable` — active state while the outfit is worn (default on). Outfits
  that don't mention the object restore its scene state at build time.

`TsiYuki.Wardrobe.BlendshapeOverride` (serializable):

- `SkinnedMeshRenderer renderer` — mesh on the avatar (typically the body).
- `string blendshape` — blendshape name on that mesh.
- `float value` — weight (0–100) applied while the outfit is worn; other
  outfits restore the value the mesh had at build time.
