using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace TsiYuki.Wardrobe
{
    public enum EntryKind
    {
        Outfit = 0,
        // Wears none of the outfits.
        None = 1,
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

    // A direct child of an outfit with its own on/off toggle. Its default is
    // the object's active state in the scene.
    [Serializable]
    public class WardrobePiece
    {
        // Stable id used in the parameter name; never derived from names, so
        // renaming the object or the label keeps saved selections.
        public string id = "";
        public GameObject target;
        public string displayName = "";
        public Texture2D icon;
    }

    [Serializable]
    public class BlendshapeOverride
    {
        // Renderer on the avatar (typically the body mesh) whose blendshape
        // is set to this value while the outfit is worn. Other outfits
        // restore the value the mesh had at build time.
        public SkinnedMeshRenderer renderer;
        public string blendshape;
        [Range(0, 100)] public float value = 100;
    }

    [Serializable]
    public class ObjectOverride
    {
        // Object elsewhere in the avatar (outside any outfit) forced to this
        // state while the outfit is worn. Outfits that don't mention it
        // restore the active state it has in the scene at build time.
        public GameObject target;
        public bool enable = true;
    }

    // One material slot that colors of an outfit replace.
    [Serializable]
    public class ColorSlot
    {
        public Renderer renderer;
        public int slot;
    }

    // One color of an outfit: a material for each of the outfit's color
    // slots (same order). The first color is the default.
    [Serializable]
    public class OutfitColor
    {
        public string displayName = "";
        public Texture2D icon;
        public List<Material> materials = new List<Material>();
    }

    // One row of the wardrobe menu: an outfit, or the "None" option.
    [Serializable]
    public class WardrobeEntry
    {
        public string id = "";

        // Value of the wardrobe's int parameter for this entry. Assigned once
        // and never reused, so reordering the list keeps saved selections.
        // 0 is reserved for "the default entry".
        public int value;

        public EntryKind kind = EntryKind.Outfit;

        // Parent object of the outfit; its direct children can be pieces.
        public GameObject root;

        // Menu label; empty uses the object name.
        public string displayName = "";
        public Texture2D icon;

        // Entries with a category are grouped into a submenu of that name.
        public string category = "";

        public List<WardrobePiece> pieces = new List<WardrobePiece>();
        public List<BlendshapeOverride> blendshapes = new List<BlendshapeOverride>();
        public List<ObjectOverride> objectOverrides = new List<ObjectOverride>();

        public OutfitMenuMode menuMode = OutfitMenuMode.Absorb;

        public List<ColorSlot> colorSlots = new List<ColorSlot>();
        public List<OutfitColor> colors = new List<OutfitColor>();

        public OutfitPlatform platform = OutfitPlatform.All;

        public WardrobePiece FindPiece(GameObject target)
        {
            foreach (var p in pieces)
                if (p != null && p.target == target) return p;
            return null;
        }
    }

    [Serializable]
    public class LookPiece
    {
        public string pieceId = "";
        public bool on = true;
    }

    // A one-click combination of an outfit and its piece toggles. Pieces a
    // look doesn't list go back to their defaults.
    [Serializable]
    public class WardrobeLook
    {
        public string displayName = "";
        public Texture2D icon;
        public string entryId = "";
        public List<LookPiece> pieces = new List<LookPiece>();
    }

    // One wardrobe menu for an avatar. Place it anywhere inside the avatar;
    // several wardrobes (for example Outfits and Hair) may coexist, each
    // becoming its own top-level menu. The build-time plugin consumes it and
    // removes it, so nothing here survives into the uploaded avatar.
    [AddComponentMenu("TsiYuki/Yuki Wardrobe")]
    [DisallowMultipleComponent]
    public class YukiWardrobe : MonoBehaviour, IEditorOnly
    {
        // Top-level menu label; empty uses the object name.
        public string displayName = "";
        public Texture2D icon;

        // Synced int parameter. Empty generates "Wardrobe/<id>".
        public string parameterName = "";
        public string id = "";

        // Whether selections persist across worlds and sessions.
        public bool saved = true;

        // Menu order. Values are stable, the order is only presentation.
        public List<WardrobeEntry> entries = new List<WardrobeEntry>();

        // Entry worn at spawn and when a menu toggle is switched off.
        public string defaultEntry = "";

        public List<WardrobeLook> looks = new List<WardrobeLook>();

        // Optional object (particles, sound) switched on for a moment each
        // time the outfit changes. Keep it disabled in the scene.
        public GameObject changeEffect;
        [Range(0.1f, 5f)] public float changeEffectDuration = 1f;

        // Next int value handed to a new entry.
        public int nextValue = 1;

        public WardrobeEntry FindEntry(string entryId)
        {
            foreach (var e in entries)
                if (e != null && e.id == entryId) return e;
            return null;
        }

        public WardrobeEntry DefaultEntry
        {
            get
            {
                var entry = FindEntry(defaultEntry);
                if (entry != null) return entry;
                foreach (var e in entries)
                    if (e != null && (e.kind == EntryKind.None || e.root != null)) return e;
                return null;
            }
        }

        public WardrobePiece FindPiece(string pieceId)
        {
            foreach (var e in entries)
                if (e != null)
                    foreach (var p in e.pieces)
                        if (p != null && p.id == pieceId) return p;
            return null;
        }

        /// <summary>
        /// Gives every entry, piece and the wardrobe a stable id and every entry
        /// a unique non-zero value. Returns true when anything changed.
        /// </summary>
        public bool EnsureIds()
        {
            bool changed = false;
            if (string.IsNullOrEmpty(id)) { id = NewId(); changed = true; }
            if (nextValue < 1) { nextValue = 1; changed = true; }

            var ids = new HashSet<string>();
            var values = new HashSet<int>();
            foreach (var e in entries)
            {
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.id) || !ids.Add(e.id)) { e.id = Unique(ids); changed = true; }
                if (e.value <= 0 || e.value > 255 || !values.Add(e.value))
                {
                    while (values.Contains(nextValue) || nextValue <= 0) nextValue++;
                    e.value = nextValue++;
                    values.Add(e.value);
                    changed = true;
                }
                if (e.value >= nextValue) { nextValue = e.value + 1; changed = true; }
            }

            var pieceIds = new HashSet<string>();
            foreach (var e in entries)
            {
                if (e == null) continue;
                foreach (var p in e.pieces)
                    if (p != null && (string.IsNullOrEmpty(p.id) || !pieceIds.Add(p.id))) { p.id = Unique(pieceIds); changed = true; }
                // Colors always have one material per slot.
                foreach (var c in e.colors)
                {
                    if (c == null) continue;
                    while (c.materials.Count < e.colorSlots.Count) { c.materials.Add(null); changed = true; }
                    while (c.materials.Count > e.colorSlots.Count) { c.materials.RemoveAt(c.materials.Count - 1); changed = true; }
                }
            }
            return changed;
        }

        public static string NewId() => Guid.NewGuid().ToString("N").Substring(0, 6);

        static string Unique(HashSet<string> used)
        {
            string candidate;
            do candidate = NewId(); while (!used.Add(candidate));
            return candidate;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (EnsureIds()) UnityEditor.EditorUtility.SetDirty(this);
        }

        void Reset()
        {
            id = NewId();
            nextValue = 1;
        }
#endif
    }
}
