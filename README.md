# Yuki Wardrobe

Outfit switcher for VRChat avatars. Put one **Yuki Wardrobe** component inside your avatar, list your
outfits, and at build time (Play mode or upload) an NDMF plugin generates the FX layer, synced
parameters and expressions menu as Modular Avatar components. Nothing is written to `Assets/`;
remove the component and the avatar is exactly as before.

## Features (2.0)

- Mutually exclusive outfits driven by one int parameter; the first outfit is the default.
- Optional per-piece toggles (one synced bool each).
- Per-outfit blendshape overrides (e.g. shrink the body under tight clothes) and object overrides,
  at no extra parameter cost.
- Categories as submenus, automatic menu paging.
- Dockable editor window: **TsiYuki > Wardrobe Editor**.

Version 3.0 (in development) adds absorbing outfits' own Modular Avatar menus, multiple wardrobes per
avatar (e.g. clothes and hair), custom display names, conflict checks and a new editor UI.

## Install

Add the TsiYuki VPM listing to VCC / ALCOM from <https://tsiyukino.github.io/vpm-repos/>, then add
**Yuki Wardrobe**. Requires the VRChat Avatars SDK, NDMF and Modular Avatar.

## License

MIT

---

## 中文

VRChat 模型的换装系统。在模型里放一个 **Yuki Wardrobe** 组件、列出衣服，进 Play 或上传时由 NDMF
自动生成 FX 层、同步参数和菜单（以 Modular Avatar 组件的形式），不会往 `Assets/` 写任何文件；
删掉组件，模型就回到原样。

安装：在 VCC / ALCOM 中添加 <https://tsiyukino.github.io/vpm-repos/>，再添加 **Yuki Wardrobe**。
