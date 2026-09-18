# WardrobeManager (Wardrobe.cs)

`VRLabs.AV3Manager.WardrobeManager : EditorWindow` — the entire tool. Opened
via the `TsiYuki/Wardrobe Manager` menu item.

## Public surface

- `static void ShowWindow()` — opens the editor window. This is the only public
  member; everything else is private to the window.

## Private structure (for orientation, not a contract)

- `WardrobeGroupInfo` — serializable record of one clothing group: parent
  GameObject, category string, refreshed child list.
- `SaveWardrobeState()` / `LoadWardrobeState()` — EditorPrefs round-trip of the
  group list, keyed by avatar instance ID.
- `GenerateWardrobeSystem()` — orchestrates cleanup and the five `Create*`
  steps below.
- `CreateFolderStructure()` — output folders under `Assets/TsiYukiData/Wardrobe/`.
- `CreateWardrobeAnimations()` — one all-off clip plus one clip per group that
  enables it and disables the others.
- `CreateWardrobeController()` — FX controller with a single int parameter
  `WardrobeState`, one state per group, transitions wired around a
  `default`-category group and a None state.
- `CreateWardrobeParameters()` — `VRCExpressionParameters` asset with the one
  saved int.
- `CreateWardrobeMenus()` — flat `VRCExpressionsMenu` with a toggle per group.
- `SanitizeParameterName(string)`, `GetRelativePath(Transform, Transform)` —
  naming and path helpers.
