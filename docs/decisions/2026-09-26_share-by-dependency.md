# Share code between the TsiYuki tools by what it depends on

Yuki Wardrobe and Yuki Material each carried their own copy of the same
editor code: `UndoEdit`, the animator helpers (add a layer, add a state, add
an Any State entry), the Modular Avatar menu-item helpers, `MenuPlacement`,
the NDMF localizer adapter and the `Report` helper. Yuki Toggle would have
made a third copy. The obvious home is TsiYuki Core, but Core deliberately
references neither Modular Avatar nor the VRChat SDK (Core 0.3.0), so the
MA-dependent pieces cannot go there.

Decision: each piece goes where its own dependencies allow, not where the
rest of the group goes.

- **Needs only UnityEditor or NDMF → TsiYuki Core.** `UndoEdit` and
  `AnimatorGraph` (Core 0.4.0), then the NDMF report helper. Core already
  references NDMF, and NDMF error reporting was already in its remit.
- **Needs Modular Avatar → a separate package**,
  `moe.tsiyuki.core.modular-avatar`: the menu-item helpers, the Menu Install
  Target reflection and `MenuPlacement`, with the placement warnings' text.
  Its `package.json` declares MA and the VRChat SDK as dependencies, so VCC
  enforces the version range in one place.

Rejected: an extra assembly inside Core compiled only when MA is installed
(`versionDefines` + `defineConstraints`). It saves a repository, but the MA
version range would live in an asmdef VCC never reads, repeated from every
tool's `package.json`. If the two drift, the assembly quietly stops compiling
and the tools fail with missing types instead of a resolution error. Core's
"references neither" would also stop being true of the package as a whole.

Not moved: `YukiMenuRegistry` and its NDMF pass stay in Core. The registry
takes the wiring as a callback precisely so it needs no MA, which keeps it
testable on its own, and every tool orders its pass against the plugin name
`moe.tsiyuki.core`.

Done in three steps so each is checked against the generated output before
the next: (1) `UndoEdit` and `AnimatorGraph`; (2) the NDMF report helper;
(3) the Modular Avatar package. Step 1 left every generated FX controller
identical. Step 2 turned out to hold a third copy of the localization file
parser, one per tool's NDMF adapter; `YukiNdmfReport` reads the tables
`YukiLocalizer` already has instead, and every message in every language came
out the same.
