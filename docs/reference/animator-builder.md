# AnimatorBuilder (Editor/AnimatorBuilder.cs)

Builds the wardrobe FX controller in memory.

- `static AnimatorController Build(WardrobeModel model, Action<Object> persist)`
  — returns the controller; calls `persist` on every created object
  (controller, state machines, states, transitions, clips) so the caller can
  register them with an asset container.

Layers, states and the Any State entry transitions are made with TsiYuki Core
Animation's `AnimatorGraph`, which Yuki Material uses too. A state is handed to `persist`
before it is attached to its state machine; a repeated state name gets a
suffix, as in the Animator window.

Also public, for the preview and the window:

- `static AnimationClip BuildOutfitClip(WardrobeModel model, ResolvedOutfit worn)` — the pose of one outfit
  (None when `worn` is null).
- `static void SetActiveCurve(AnimationClip clip, string path, bool active)`,
  `static void SetBlendshapeCurve(AnimationClip clip, BlendshapeChannel channel, float weight)`.
- `const string OneParameter = "TsiYuki/Wardrobe/One"`.

Output shape:

- Parameters: `<param>` (int); when there are pieces, one float per piece (the synced bool arrives as a float,
  which blend trees need; default from `DefaultOn`) and `TsiYuki/Wardrobe/One` (float 1); one int per outfit
  with colors; `<param>/Look` (int) when there are looks.
- **Outfit layer** (`<param>`): a state per entry (`None` for the None entry), each clip animating every
  outfit root (its own on, the rest off), every object and blendshape channel (the entry's forced value, or
  the channel baseline so nothing lingers across switches) and, when set, the change effect (on, then off after
  its duration). Any State transitions on `<param>` = the entry's value; the default entry is the default state
  and is also entered on 0, which is what VRChat resets the parameter to.
- **Pieces layer** (`<param>/Pieces`), when there are pieces: one state holding a Direct blend tree, with a 1D
  tree per piece on its float, Off at 0 and On at 1, each weighted by `TsiYuki/Wardrobe/One`.
- **Color layer** per outfit with colors (the outfit's color int): a state per color swapping the materials of
  the slots that color changes; color 0 is the default state.
- **Looks layer** (`<param>/Look`), when there are looks: an Idle default state and a state per look whose
  local-only parameter driver sets `<param>` and every piece the look names; it returns to Idle as soon as the
  Look parameter changes.
- Write defaults off on every state; every state of a layer animates everything that layer touches.
