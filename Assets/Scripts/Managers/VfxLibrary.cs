using System;
using System.Collections.Generic;
using UnityEngine;

// The effect, size and placement of every gameplay action. Tune them on Assets/Resources/VfxLibrary.
public class VfxLibrary : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public VfxId id;
        public GameObject prefab;
        [Tooltip("Plugin effects are authored 3-25m wide, a floor tile is 1m.")]
        public float scale = 1f;
        public Vector3 offset;
        [Tooltip("Seconds before a one-shot is removed. Looping effects ignore this.")]
        public float lifetime = 2f;
        [Tooltip("Hides child systems with Smoke in the name, they cover the tile at this scale.")]
        public bool hideSmoke;
        [Tooltip("Tints the main particle system. Leave alpha at 0 to keep the plugin colors.")]
        public Color tint = new Color(1f, 1f, 1f, 0f);
    }

    public Material wetOverlayMaterial;
    public List<Entry> entries = new();
}
