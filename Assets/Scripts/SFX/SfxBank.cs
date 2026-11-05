using UnityEngine;

// Sounds that aren't tied to a prefab: menu buttons, level stingers and warnings.
// Lives at Assets/Resources/SfxBank so SfxBootstrap can load it without any scene setup.
public class SfxBank : ScriptableObject
{
    public AudioClip[] buttonHover;
    public AudioClip[] buttonClick;
    public AudioClip[] defeat;
    public AudioClip[] warning;
    [Range(0f, 1f)] public float uiVolume = 0.55f;
    [Range(0f, 1f)] public float stingerVolume = 0.8f;
}
