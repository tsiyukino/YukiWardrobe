using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace TsiYuki.Wardrobe
{
    [Serializable]
    public class ObjectOverride
    {
        // Object elsewhere in the avatar (outside any outfit root) forced to
        // this state while the outfit is worn. Outfits that don't mention an
        // object restore the active state it has in the scene at build time.
        public GameObject target;
        public bool enable = true;
    }

    [Serializable]
    public class BlendshapeOverride
    {
        // Renderer on the avatar (typically the body mesh) whose blendshape
        // is set to this value while the outfit is worn. Other outfits
        // restore the value the mesh had at build time.
        public SkinnedMeshRenderer renderer;
        public string blendshape;
        [Range(0, 100)] public float value;
    }

    // Menu name and icon of one toggleable piece. Pieces without an entry use
    // the object name and no icon.
    [Serializable]
    public class PieceSettings
    {
        public GameObject target;
        public string displayName = "";
        public Texture2D icon;
    }

    // What happens to Modular Avatar menus that ship inside an outfit
    // (for example a "Cloth Change" submenu with its own toggles).
    public enum OutfitMenuMode
    {
        // Moved into this outfit's wardrobe submenu. Parameters are untouched.
        Absorb = 0,
        // Installed where the outfit author intended (usually the root menu).
        Keep = 1,
        // Not installed at all. The toggles keep their default values.
        Hide = 2,
    }

    public enum OutfitPlatform
    {
        All = 0,
        PCOnly = 1,
        MobileOnly = 2,
    }

    // Replaces one material slot while a color / material variant is selected.
    [Serializable]
    public class MaterialSlotOverride
    {
        public Renderer renderer;
        public int slot;
        public Material material;
    }

    [Serializable]
    public class OutfitVariant
    {
        public string displayName = "";
        public Texture2D icon;
        public List<MaterialSlotOverride> materials = new List<MaterialSlotOverride>();
    }

    [Serializable]
    public class OutfitGroup
    {
        // Parent GameObject whose direct children are the pieces of this outfit.
        public GameObject root;

        // Menu label; empty uses the root's name. Changing it never changes
        // parameter names, so saved selections survive renames.
        public string displayName = "";
        public Texture2D icon;

        public string category = "Default";

        // Direct children of root that get their own bool parameter and menu
        // toggle. Empty means the outfit is a single on/off unit.
        public List<GameObject> toggleablePieces = new List<GameObject>();
        public List<PieceSettings> pieceSettings = new List<PieceSettings>();

        // Avatar blendshapes to set while this outfit is worn.
        public List<BlendshapeOverride> blendshapes = new List<BlendshapeOverride>();

        // Objects elsewhere in the avatar forced on or off while this outfit
        // is worn — e.g. underwear an outfit needs (on) or a bandage a tight
        // outfit clips through (off).
        public List<ObjectOverride> objectOverrides = new List<ObjectOverride>();

        public OutfitMenuMode menuMode = OutfitMenuMode.Absorb;

        // Color / material variants. The first entry is the default; an empty
        // list means the outfit has no variants.
        public List<OutfitVariant> variants = new List<OutfitVariant>();

        public OutfitPlatform platform = OutfitPlatform.All;

        public PieceSettings FindPiece(GameObject piece)
        {
            if (pieceSettings == null) return null;
            foreach (var p in pieceSettings)
                if (p != null && p.target == piece) return p;
            return null;
        }
    }

    [Serializable]
    public class LookPiece
    {
        public GameObject piece;
        public bool on = true;
    }

    // A one-click combination of an outfit and its piece toggles.
    [Serializable]
    public class WardrobeLook
    {
        public string displayName = "";
        public Texture2D icon;
        public GameObject outfit;
        public List<LookPiece> pieces = new List<LookPiece>();
    }

    // One wardrobe menu for an avatar. Place it anywhere inside the avatar
    // hierarchy; several wardrobes (e.g. "Outfits" and "Hair") may coexist,
    // each becoming its own top-level menu. The build-time plugin consumes
    // it and removes it, so nothing here survives into the uploaded avatar.
    [AddComponentMenu("TsiYuki/Yuki Wardrobe")]
    [DisallowMultipleComponent]
    public class YukiWardrobe : MonoBehaviour, IEditorOnly
    {
        // Order matters: the first outfit is the default worn at spawn and
        // the fallback when a menu toggle is switched off.
        public List<OutfitGroup> outfits = new List<OutfitGroup>();

        // Adds a "None" (nothing worn) option to the menu.
        public bool includeNone = false;
        public string noneLabel = "";
        public Texture2D noneIcon;

        // Synced int parameter. Empty generates a unique name from the
        // object name. Wardrobes made with 2.x keep "WardrobeState".
        public string parameterName = "";

        // Label of the top-level menu; empty uses the object name.
        public string menuName = "";
        public Texture2D menuIcon;

        // Whether parameter values persist across worlds/sessions.
        public bool saved = true;

        public List<WardrobeLook> looks = new List<WardrobeLook>();

        // Optional object (particles, sound) switched on for a moment each
        // time the outfit changes. Keep it disabled in the scene.
        public GameObject changeEffect;
        [Range(0.1f, 5f)] public float changeEffectDuration = 1f;
    }
}
