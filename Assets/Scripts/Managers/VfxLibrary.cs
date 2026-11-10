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
        [Tooltip("Scales the start size of child systems with Smoke in the name (when not hidden).")]
        public float smokeSize = 1f;
        [Tooltip("Tints child systems with Smoke in the name. Leave alpha at 0 to keep the plugin colors.")]
        public Color smokeTint = new Color(1f, 1f, 1f, 0f);
        [Tooltip("Tints the main particle system. Leave alpha at 0 to keep the plugin colors.")]
        public Color tint = new Color(1f, 1f, 1f, 0f);

        [Header("Sound")]
        [Tooltip("One is picked at random each time. Looping effects use the first clip as a loop.")]
        public AudioClip[] sounds;
        [Range(0f, 1f)] public float volume = 0.8f;
        [Range(0f, 0.3f)] public float pitchJitter = 0.06f;
        [Tooltip("0 = flat (stingers, celebrations), 1 = fully positional.")]
        [Range(0f, 1f)] public float spatial = 0.6f;
    }

    public Material wetOverlayMaterial;
    public List<Entry> entries = new();
}
