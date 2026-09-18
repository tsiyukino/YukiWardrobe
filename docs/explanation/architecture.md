# Architecture

Yuki Wardrobe is a UPM/VPM package for VRChat avatars. You put one
`YukiWardrobe` component somewhere inside an avatar, list your outfits in it,
and at build time (entering play mode or uploading) an NDMF plugin turns that
configuration into Modular Avatar components: a merged FX layer, synced
expression parameters, and an installed menu. Nothing is written to `Assets/`
and nothing on the avatar is modified destructively; delete the component and
the avatar is exactly as it was.

Outfits are mutually exclusive, driven by one int parameter (`WardrobeState`
by default). An outfit is a parent GameObject; its direct children are the
pieces. Individual pieces can be marked toggleable, which adds one synced bool
parameter and one menu toggle per marked piece so wearers can hide individual
parts; outfits with no marked pieces stay a single menu toggle. A "None"
(nothing worn) option is included by default and can be turned off.

## Module map

Runtime assembly (`TsiYuki.Wardrobe`):

- `YukiWardrobe` — the configuration component. Pure data, `IEditorOnly`, one
  per avatar, placeable anywhere in the hierarchy.

Editor assembly (`TsiYuki.Wardrobe.Editor`):

- `WardrobeModel` — resolves the component into an immutable build model:
  state index assignment, avatar-relative paths, sanitized unique parameter
  names, warnings, parameter-bit total. Every other editor module consumes
  this instead of reading the component directly.
- `AnimatorBuilder` — model → in-memory `AnimatorController`: one
  exclusive-switch layer (Any State transitions on the int) plus a two-state
  layer per piece toggle. Write defaults off everywhere; every state animates
  the full property set of its layer, so the controller is WD-agnostic.
- `MenuBuilder` — model → `VRCExpressionsMenu` tree: outfits in the "Default"
  category sit at the wardrobe root, every other category gets a submenu;
  per-outfit submenus (Wear + piece toggles) for outfits with marked pieces;
  automatic pagination past VRChat's per-menu control cap.
- `WardrobePlugin` — the NDMF plugin. Generating phase, one pass: resolve
  model, run both builders, attach `ModularAvatarMergeAnimator`,
  `ModularAvatarParameters`, and `ModularAvatarMenuInstaller` to the
  component's GameObject, then destroy the component. Asset persistence goes
  through NDMF's asset container via a callback handed to the builders.
- `WardrobeWindow` — dockable editing UI. Owns no wardrobe data; every edit
  writes to the component through Undo, so closing the window or restarting
  the editor loses nothing. The outfit list itself (reorder, piece
  checkboxes, blendshape overrides) lives in `OutfitListGUI`.
- `YukiWardrobeEditor` — component inspector, summary and a button to open
  the window.

## Dependency direction

```
WardrobeWindow / YukiWardrobeEditor ──▶ WardrobeModel ──▶ YukiWardrobe (data)
WardrobePlugin ──▶ AnimatorBuilder / MenuBuilder ──▶ WardrobeModel
```

Builders never touch NDMF or MA types; the plugin never builds animator or
menu content itself. The runtime assembly depends only on the VRC SDK base.

## Design points worth knowing

- **Index scheme.** Outfits keep their list order: outfit *i* gets index *i*,
  so the first outfit owns index 0 — the value VRChat resets a toggled-off
  parameter to — making it both the spawn default and the toggle-off
  fallback. There is no separate "default" setting; reordering the list (drag
  in the window) is how the default changes. None, when enabled, takes the
  index above every outfit. `WardrobeModel` is the only place indices are
  assigned.
- **Per-piece defaults come from the scene.** A piece that is inactive in the
  editor starts hidden in game: its bool parameter default, animator default,
  and layer default state all derive from `activeSelf` at build time.
- **Blendshape overrides ride the int.** An outfit can set avatar blendshapes
  (e.g. shrink the body under tight clothes). Every outfit state animates the
  union of all touched channels — the outfit's own value, or the value the
  mesh had at build time — so switching outfits always restores what it
  should, and no parameter memory is spent.
- **Object overrides ride the int the same way.** An outfit can force objects
  outside its own root on (underwear it needs) or off (a bandage it clips
  through). Every outfit state animates the union of all overridden objects —
  the current outfit's forced state, or the object's scene state at build
  time — so an object an outfit doesn't mention looks exactly as it does in
  the editor. Objects inside an outfit root are rejected as targets; they are
  already controlled by the switch.
- **Why Modular Avatar primitives instead of writing into the FX controller
  directly:** animator merging, parameter injection, and menu installation
  already exist in MA and are battle-tested; the plugin only has to produce
  inputs for them. Running in the Generating phase guarantees MA's
  Transforming-phase passes see the generated components.
- **Parameter cost** is 8 bits for the int plus 1 bit per piece toggle; the
  window shows the total against `VRCExpressionParameters.MAX_PARAMETER_COST`.

## Legacy

The previous implementation (a single `Wardrobe.cs` EditorWindow that wrote
shared assets to `Assets/TsiYukiData/Wardrobe/` and kept its state in
EditorPrefs) is superseded; see
[decisions/2026-07-15_component-ndmf-rewrite.md](../decisions/2026-07-15_component-ndmf-rewrite.md)
for why it was replaced rather than patched.
