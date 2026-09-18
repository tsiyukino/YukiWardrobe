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

    [Serializable]
    public class OutfitGroup
    {
        // Parent GameObject whose direct children are the pieces of this outfit.
        public GameObject root;

        public string category = "Default";

        // Direct children of root that get their own bool parameter and menu
        // toggle. Empty means the outfit is a single on/off unit.
        public List<GameObject> toggleablePieces = new List<GameObject>();

        // Avatar blendshapes to set while this outfit is worn.
        public List<BlendshapeOverride> blendshapes = new List<BlendshapeOverride>();

        // Objects elsewhere in the avatar forced on or off while this outfit
        // is worn — e.g. underwear an outfit needs (on) or a bandage a tight
        // outfit clips through (off).
        public List<ObjectOverride> objectOverrides = new List<ObjectOverride>();
    }

    // Wardrobe configuration for one avatar. Place a single instance anywhere
    // inside the avatar hierarchy; the build-time plugin consumes it and
    // removes it, so nothing here survives into the uploaded avatar.
    [AddComponentMenu("TsiYuki/Yuki Wardrobe")]
    [DisallowMultipleComponent]
    public class YukiWardrobe : MonoBehaviour, IEditorOnly
    {
        // Order matters: the first outfit is the default worn at spawn and
        // the fallback when a menu toggle is switched off.
        public List<OutfitGroup> outfits = new List<OutfitGroup>();

        // Adds a "None" (nothing worn) option to the menu.
        public bool includeNone = true;

        public string parameterName = "WardrobeState";

        // Label of the wardrobe entry added to the avatar's expressions menu.
        public string menuName = "Wardrobe";

        // Whether parameter values persist across worlds/sessions.
        public bool saved = true;
    }
}
