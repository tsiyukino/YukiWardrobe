# Share code between the TsiYuki tools by field, not by tool count

Yuki Wardrobe and Yuki Material each carried their own copy of the same
editor code: `UndoEdit`, the animator helpers (add a layer, add a state, add
an Any State entry), the Modular Avatar menu-item helpers, `MenuPlacement`,
and the NDMF localizer adapter with its `Report` helper. Yuki Follow and Yuki
Menu had the NDMF adapter too, and Yuki Toggle would have copied most of it
again.

## Decision

TsiYuki Core holds only what nearly every tool needs, or an agreement that
only works as one shared copy. Everything else lives in a TsiYuki Core package
for its field, which VCC installs only with the tools that depend on it:

- `moe.tsiyuki.core` — language, localization, GUI, `UndoEdit`,
  `YukiNdmfReport`. No Modular Avatar, no VRChat SDK.
- `moe.tsiyuki.core.animation` — `AnimatorGraph`, for tools that generate FX
  layers.
- `moe.tsiyuki.core.texture` — `Pixels` and `TextureBaker`, for tools that bake
  textures and materials.
- `moe.tsiyuki.core.menus` — `MenuItems`, `InstallTarget`, `MenuPlacement` and
  the registry that lets one tool's menu go inside another's. It depends on
  Modular Avatar and the VRChat SDK, which is what menus are made of.

A field gets its own package when a second tool in it needs the code. Before
that the code stays in the one tool that has it, even if it would fit a field
package.

The rule is written down in TsiYuki Core's README, since that is where the
next tool's author looks first.

## Why

Users install one or two TsiYuki tools, and the fields barely overlap: an
animation tool has no use for texture compositing. One Core holding
everything would make every install carry, and every Core release touch, code
most users never run. But no field has only one tool, so copying the code into
each tool would leave copies that drift apart — the two `AddState` copies had
already diverged, one of them quietly writing to the asset file and the undo
stack during a build.

Rejected along the way:

- **A single "Core for Modular Avatar" package** for all MA-dependent code.
  It groups by dependency rather than by field, so a tool that builds menus
  but never animates would still get whatever else needed MA.
- **An optional assembly inside Core, compiled only when MA is installed.**
  The MA version range would live in an asmdef that VCC never reads,
  repeated from every tool's `package.json`; when the two drift the assembly
  quietly stops compiling and tools fail with missing types instead of a
  clear resolution error.
- **Keeping per-tool copies.** Cheaper today, but each field already has two
  tools and more coming.

## Consequences

- Moving the registry into `core.menus`, next to Modular Avatar, removed the
  callbacks that existed only because Core could not see MA: tools call
  `MenuPlacement.Place` and nothing else, and the placement warnings are the
  package's own. It also fixed the registry never clearing its table between
  builds.
- Tools order their pass before `moe.tsiyuki.core.menus` (previously
  `moe.tsiyuki.core`).
- Each step was checked against generated output: every FX controller, every
  NDMF error message in all three languages, and the final expressions menu,
  parameters and materials of the test avatars came out the same.
