# Docs index

## Explanation
- [architecture.md](explanation/architecture.md) — what the package does, the module map, dependency direction, and the design points worth knowing.

## Reference
- [yuki-wardrobe.md](reference/yuki-wardrobe.md) — the configuration component (runtime assembly).
- [wardrobe-model.md](reference/wardrobe-model.md) — build model: index assignment, paths, parameter names, warnings.
- [animator-builder.md](reference/animator-builder.md) — FX controller generation.
- [menu-generator.md](reference/menu-generator.md) — the menu, as Modular Avatar menu items.
- [wardrobe-plugin.md](reference/wardrobe-plugin.md) — the NDMF pass that emits Modular Avatar components and the parameters it declares.
- [wardrobe-window.md](reference/wardrobe-window.md) — the dockable editing window.
- [yuki-wardrobe-editor.md](reference/yuki-wardrobe-editor.md) — the summary inspector.

## Decisions
- [2026-07-15_component-ndmf-rewrite.md](decisions/2026-07-15_component-ndmf-rewrite.md) — why the tool was rewritten as a component + NDMF plugin.
- [2026-09-26_share-by-field.md](decisions/2026-09-26_share-by-field.md) — why shared code lives in TsiYuki Core and one Core package per field (animation, texture, menus) rather than in each tool.
