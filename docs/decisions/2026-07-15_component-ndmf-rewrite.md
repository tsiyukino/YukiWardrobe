# Rewrite as component + NDMF plugin instead of patching the EditorWindow

The old tool had three complaints from daily use: every avatar shared one
generated FX/parameters/menu folder (generation for one avatar deleted the
others'), a configured wardrobe could not be reloaded later, and the generated
assets had to be dragged onto Modular Avatar components by hand. All three
traced back to architecture, not bugs: a fixed output path wiped on every run,
state keyed by `GetInstanceID()` in EditorPrefs (unstable across editor
sessions, invisible to version control), and generation that stopped at loose
assets.

Decision: store configuration as a `YukiWardrobe` component inside the avatar
and generate everything at build time through an NDMF plugin that emits
Modular Avatar components. Consequences that drove the choice:

- Component data is serialized with the scene/prefab, so persistence,
  version control, and per-avatar isolation come for free instead of being
  features to build.
- Build-time generation into NDMF's asset container means no files in
  `Assets/`, no cleanup step, and no way for two avatars to collide.
- Emitting MA Merge Animator / Parameters / Menu Installer reuses MA's
  battle-tested merging instead of reimplementing it, and removes the manual
  wiring step entirely.

Also decided here:

- **Index 0 = toggle-off fallback.** VRChat resets a toggled-off parameter to
  its default (0), so the default outfit must own index 0. Encoded once, in
  `WardrobeModel`.
- **The editing window is a view, not a store.** The old window owned the
  data, which is how state got lost. The new one edits the component through
  Undo and keeps only "which avatar am I looking at" as window state. A
  window was kept at all because Inspector-based editing loses focus the
  moment you click the Hierarchy to drag an outfit in.
- **Per-piece toggles are opt-in per outfit** (one synced bool per piece, 1
  bit each) with defaults taken from scene `activeSelf`.
- No migration from the old tool: the EditorPrefs data is keyed by dead
  instance IDs and effectively unrecoverable. Outfits are re-added once via
  the new window.

Revised the same day after first use:

- **The default outfit is the first list entry, not a setting.** A separate
  default field could point at a deleted outfit; list order can't dangle, and
  drag-reordering doubles as choosing the default.
- **Per-piece toggles are chosen piece by piece** (`toggleablePieces` list)
  instead of one per-outfit flag; an outfit with nothing marked stays a plain
  menu toggle.
- **None is optional** (`includeNone`), on by default.
