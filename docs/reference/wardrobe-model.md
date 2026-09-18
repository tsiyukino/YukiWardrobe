# WardrobeModel (Editor/WardrobeModel.cs)

Resolves a `YukiWardrobe` component into the model every other editor module
consumes. State index assignment lives here and nowhere else: outfits keep
their list order (outfit *i* → index *i*), so the first outfit owns index 0,
which is the spawn default and the toggle-off fallback; None, when enabled,
takes the index above every outfit.

- `static WardrobeModel Resolve(YukiWardrobe config, Transform avatarRoot)` —
  validates outfits (null, outside-avatar, and duplicate roots are skipped
  with a warning) and toggleable pieces (must be direct children of their
  outfit root), assigns indices, computes avatar-relative paths and unique
  sanitized parameter names, and captures each piece's `activeSelf` as its
  default. Elements are emitted in hierarchy order regardless of selection
  order.

Model surface:

- `string ParameterName`, `string MenuName`, `bool Saved` — settings with
  fallbacks applied.
- `bool IncludeNone`, `int NoneIndex` — the naked option; `NoneIndex` is
  meaningful only when `IncludeNone`.
- `List<ResolvedOutfit> Outfits` — `DisplayName`, `Index`, `Category`, `Path`,
  `Root`, `Elements`, `BlendshapeValues` (channel key → weight).
- `ResolvedElement` — `DisplayName`, `ParameterName`
  (`<param>/<outfit>/<piece>`), `Path`, `DefaultOn`.
- `List<BlendshapeChannel> BlendshapeChannels` — the union of avatar
  blendshapes touched by any outfit: `Path`, `Blendshape`, `Baseline` (editor
  value captured at resolve time), plus derived `Property` and `Key`.
  Overrides pointing outside the avatar or at a nonexistent blendshape are
  skipped with a warning.
- `List<ObjectChannel> ObjectChannels` — the union of avatar objects forced
  on/off by any outfit: `Path` plus `Baseline` (scene active state at resolve
  time); each `ResolvedOutfit.ObjectValues` maps channel path → forced state.
  Targets outside the avatar or inside any outfit root (already controlled by
  outfit switching) are skipped with a warning.
- `List<string> Warnings` — human-readable validation messages.
- `int TotalBits` — 8 for the int plus 1 per toggleable piece.
