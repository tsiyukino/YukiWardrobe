# Yuki Wardrobe

Outfit (and hair) switcher for VRChat avatars. Put a **Yuki Wardrobe** object inside your avatar, add your
outfits, and at build time (Play mode or upload) an NDMF plugin generates the FX layers, synced parameters and
menu as Modular Avatar components. Nothing is written to your assets; remove the component and the avatar is
exactly as before.

[English](#english) · [中文](#中文) · [日本語](#日本語)

## English

### Install

Add the TsiYuki VPM listing to VCC / ALCOM: `https://tsiyukino.github.io/vpm-repos/index.json`, then add
**Yuki Wardrobe**. Requires the VRChat Avatars SDK, NDMF and Modular Avatar.

### Use

1. **TsiYuki > Wardrobe Editor**, pick your avatar, click *Create a wardrobe*.
2. Drop outfit prefabs (or objects already on the avatar) on the drop area. Prefabs are placed and set up
   with MA Setup Outfit. You can also right-click objects in the Hierarchy: *TsiYuki > Add to Wardrobe*.
3. The top outfit is the default. Use the tabs to pick toggleable pieces, body blendshapes, other objects,
   colors, and what happens to the outfit's own menus (moved into its submenu by default).
4. Press ▶ to try an outfit on in the scene; *Menu preview* shows the menu that will be generated; *Checks*
   lists conflicts with other tools.
5. Add another wardrobe with **+** for hair or accessories — each wardrobe is its own top-level menu.

### Features

- Exclusive outfits on one int parameter, optional per-piece toggles, colors (material variants).
- Outfit menus absorbed into the wardrobe, so they don't clutter the root menu.
- Looks: one-click combinations that cost no synced parameters.
- Blendshape and object overrides per outfit at no parameter cost.
- Conflict checks and a whole-avatar parameter budget.
- Try-on preview, generated menu icons, PC-only / mobile-only outfits, change effect.
- English / 中文 / 日本語 UI.

## 中文

VRChat 模型的换装（和发型）系统。在模型里放一个 **Yuki Wardrobe**，加入衣服，进 Play 或上传时由 NDMF 自动生成
FX 层、同步参数和菜单（Modular Avatar 组件），不会改动你的资源；删掉组件，模型就回到原样。

1. **TsiYuki > Wardrobe Editor**，选择模型，点“创建衣柜”。
2. 把衣服 Prefab（或模型上已有的物体）拖到拖放区。Prefab 会自动放进模型并执行 MA Setup Outfit。也可以在 Hierarchy 右键 *TsiYuki > Add to Wardrobe*。
3. 最上面的衣服是默认。用标签页设置单件、身体 blendshape、其它物体、颜色，以及衣服自带菜单怎么处理（默认移到这套衣服的子菜单里）。
4. 点 ▶ 在场景里试穿；“菜单预览”显示最终菜单；“检查”列出和其它工具的冲突。
5. 用 **+** 再加一个衣柜放发型或配件，每个衣柜都是独立的顶层菜单。

## 日本語

VRChat アバター用の衣装（と髪型）切り替えツールです。アバターに **Yuki Wardrobe** を置いて衣装を追加すると、Play
またはアップロード時に NDMF が FX レイヤー・同期パラメーター・メニューを Modular Avatar コンポーネントとして生成します。
アセットは変更しません。

1. **TsiYuki > Wardrobe Editor** でアバターを選び、「衣装棚を作成」。
2. 衣装の Prefab（またはアバター上のオブジェクト）をドロップ。Prefab は配置後に MA Setup Outfit を実行します。Hierarchy の右クリック *TsiYuki > Add to Wardrobe* でも追加できます。
3. 一番上の衣装がデフォルトです。タブでパーツ・素体シェイプキー・その他オブジェクト・カラー・衣装付属メニューの扱い（デフォルトはこの衣装のサブメニューへ移動）を設定します。
4. ▶ でシーン上で試着、「メニュープレビュー」で生成されるメニュー、「チェック」で他ツールとの競合を確認できます。
5. **+** で髪型やアクセサリー用の衣装棚を追加できます。衣装棚ごとに独立したトップメニューになります。

## License

MIT
