using System.Collections.Generic;
using CartoonFX;
using UnityEngine;

public enum VfxId
{
    TilePlaced,
    TileNail,
    TileFinished,
    RoomFinished,
    CutterCutting,
    ProductOut,
    MixerDone,
    MixerFail,
    Refuel,
    OutOfPower,
    FireIgnite,
    FireBurn,
    FireOut,
    TileDestroyedWood,
    TileDestroyedBrick,
    TileDestroyedGlass,
    PipeBurst,
    WoodSwell,
    SocketSpark,
    TileElectrified,
    PlayerElectrocuted,
    PlayerBurned,
    CarHit,
    PlayerSlip,
    PlayerStun,
    PlayerDrink,
    PlayerEnergised,
    PlayerDrained,
    PlayerRespawn,
    DogEat,
    DogPet,
    DogGift,
    DogRebel,
    DogMoodWorse,
    BowlFill,
    MailCollect,
    Victory,
    PoleImpact,
    PuddleElectrified,
    PipeWarning,
    SocketWarning,
    HazardRepaired,
}


// Each entry can also carry a sound (VfxLibrary.Entry.sounds), played through Sfx.
// Plays gameplay effects from one place, so the tile and station prefabs don't each carry their own copies.
// Singleplayer only: multiplayer has no sync for these and keeps its current visuals.
public static class VfxManager
{
    private const string LibraryPath = "VfxLibrary";

    private static VfxLibrary library;
    private static Dictionary<VfxId, VfxLibrary.Entry> entries;

    public static Material WetOverlayMaterial => Library != null ? Library.wetOverlayMaterial : null;

    private static VfxLibrary Library
    {
        get
        {
            if (library == null)
            {
                library = Resources.Load<VfxLibrary>(LibraryPath);
                entries = new Dictionary<VfxId, VfxLibrary.Entry>();
                if (library != null)
                {
                    foreach (VfxLibrary.Entry entry in library.entries) entries[entry.id] = entry;
                }
            }
            return library;
        }
    }
    public static void Play(VfxId id, Vector3 position)
    {
        GameObject effect = Spawn(id, position, null, out VfxLibrary.Entry entry);
        if (effect != null) UnityEngine.Object.Destroy(effect, entry.lifetime);
        if (entry != null) Sfx.Play(entry.sounds, position, entry.volume, entry.pitchJitter, entry.spatial);
    }
    // Stays on the target until Stop is called with what this returns.
    public static GameObject PlayLoop(VfxId id, Transform target)
    {
        GameObject effect = Spawn(id, target.position, target, out VfxLibrary.Entry entry);
        // The loop sound lives on the effect, so Stop silences it too.
        if (effect != null && entry.sounds != null && entry.sounds.Length > 0) Sfx.AttachLoop(effect, entry.sounds[0], entry.volume);
        return effect;
    }
    public static void Stop(GameObject effect)
    {
        if (effect != null) UnityEngine.Object.Destroy(effect);
    }
    private static GameObject Spawn(VfxId id, Vector3 position, Transform parent, out VfxLibrary.Entry entry)
    {
        entry = null;
        if (NetworkModeHelper.IsNetworked || Library == null) return null;
        if (!entries.TryGetValue(id, out entry)) return null;
        if (entry.prefab == null)
        {
            // Sound-only entries still need a host for their loop.
            if (entry.sounds == null || entry.sounds.Length == 0) return null;
            var host = new GameObject(id + " Sound");
            host.transform.position = position + entry.offset;
            if (parent != null) host.transform.SetParent(parent, true);
            return host;
        }

        GameObject effect = UnityEngine.Object.Instantiate(entry.prefab, position + entry.offset, Quaternion.identity);
        // Without Hierarchy mode a particle system ignores its transform's scale.
        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particles.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        // These run for every small game action: no room-wide light flash or camera shake.
        foreach (Light effectLight in effect.GetComponentsInChildren<Light>(true)) effectLight.enabled = false;
        if (effect.TryGetComponent<CFXR_Effect>(out var cfxr)) cfxr.cameraShake.enabled = false;
        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (particles.name.IndexOf("Smoke", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (entry.hideSmoke)
            {
                particles.gameObject.SetActive(false);
                continue;
            }
            ParticleSystem.MainModule smokeMain = particles.main;
            if (entry.smokeSize > 0f && !Mathf.Approximately(entry.smokeSize, 1f)) smokeMain.startSizeMultiplier *= entry.smokeSize;
            if (entry.smokeTint.a > 0f) smokeMain.startColor = entry.smokeTint;
        }
        if (entry.tint.a > 0f && effect.TryGetComponent<ParticleSystem>(out var mainSystem))
        {
            ParticleSystem.MainModule mainModule = mainSystem.main;
            mainModule.startColor = entry.tint;
        }
        effect.transform.localScale = Vector3.one * entry.scale;
        if (parent != null) effect.transform.SetParent(parent, true);
        return effect;
    }
}
