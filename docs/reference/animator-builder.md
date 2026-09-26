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

Output shape:

- One int parameter plus one bool parameter per piece toggle (default from
  `DefaultOn`).
- One exclusive-switch layer: a state per outfit, plus None when enabled,
  each state's clip animating every outfit root (own = on, rest = off) and
  every object and blendshape channel (the outfit's forced value, or the
  channel baseline so nothing lingers across switches); Any State transitions
  with
  `Equals` on the int, `canTransitionToSelf` off, zero duration; default
  state = index 0 (the first outfit).
- One layer per piece toggle: On/Off states animating just that piece,
  transitions on the bool.
- Write defaults off on every state.
