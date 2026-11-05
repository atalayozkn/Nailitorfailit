using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Builds Assets/Resources/VfxLibrary from the table below. Re-running overwrites values tuned by hand on the asset.
// Tools > NailIt > Build VFX Library
public static class VfxLibrarySetup
{
    private const string LibraryPath = "Assets/Resources/VfxLibrary.asset";
    private const string OverlayPath = "Assets/Art/Materials/TileWetOverlay.mat";
    private static readonly string[] SearchFolders = { "Assets/Plugins/cartoon fx", "Assets/Plugins/water fx" };

    private static readonly (VfxId id, string prefab, float scale, float height, float lifetime, bool hideSmoke)[] Table =
    {
        (VfxId.TilePlaced, "CFXR3 Hit Misc A", 0.2f, 0.1f, 1f, false),
        (VfxId.TileNail, "CFXR3 Hit Misc A", 0.14f, 0.1f, 1f, false),
        (VfxId.TileFinished, "CFXR Magic Poof", 0.25f, 0.2f, 1.5f, false),
        (VfxId.RoomFinished, "CFXR4 Firework 1 Cyan-Purple (HDR)", 0.8f, 2.5f, 3f, false),
        (VfxId.CutterCutting, "CFXR3 Hit Misc F Smoke", 0.15f, 0.1f, 1f, false),
        (VfxId.ProductOut, "CFXR Magic Poof", 0.15f, 0.1f, 1.5f, false),
        (VfxId.MixerDone, "CFXR3 Hit Misc F Smoke", 0.25f, 0.2f, 1f, false),
        (VfxId.MixerFail, "CFXR _BOING_", 0.25f, 0.8f, 1f, false),
        (VfxId.Refuel, "CFXR Hit D 3D (Yellow)", 0.12f, 0.5f, 1f, false),
        (VfxId.OutOfPower, "CFXR3 Hit Electric C (Air)", 0.12f, 0.5f, 1f, false),
        (VfxId.FireIgnite, "CFXR3 Fire Explosion B", 0.3f, 0.1f, 1.5f, true),
        (VfxId.FireBurn, "CFXR Fire", 1.1f, 0f, 0f, true),
        (VfxId.FireOut, "CFXR3 Hit Misc F Smoke", 0.2f, 0.2f, 1f, false),
        (VfxId.TileDestroyedWood, "CFXR3 Hit Leaves A (Lit)", 0.3f, 0.1f, 2f, false),
        (VfxId.TileDestroyedBrick, "CFXR2 Ground Hit", 0.35f, 0.1f, 1f, false),
        (VfxId.TileDestroyedGlass, "CFXR4 Sword Hit ICE (Cross)", 0.35f, 0.1f, 1f, false),
        (VfxId.PipeBurst, "Water_Splash_A", 0.5f, 0.1f, 2f, false),
        (VfxId.WoodSwell, "CFXR Water Ripples", 0.2f, 0.05f, 1.5f, false),
        (VfxId.SocketSpark, "CFXR3 Hit Electric C (Air)", 0.15f, 0f, 1f, false),
        (VfxId.TileElectrified, "CFXR Electrified 3", 0.25f, 0.15f, 0f, false),
        (VfxId.PlayerElectrocuted, "CFXR Electrified 3", 0.2f, 1f, 1.5f, false),
        (VfxId.PlayerBurned, "CFXR Fire", 1f, 0f, 1.5f, true),
        (VfxId.CarHit, "CFXR _POW_", 0.3f, 1.2f, 1f, false),
        (VfxId.PlayerSlip, "CFXR Water Splash (Smaller)", 0.3f, 0.05f, 1f, false),
        (VfxId.PlayerStun, "CFXR4 Falling Stars", 0.3f, 1.5f, 0f, false),
        (VfxId.PlayerDrink, "CFXR3 LightGlow A (Loop)", 0.35f, 1f, 1f, false),
        (VfxId.PlayerEnergised, "CFXR3 Magic Aura A (Runic)", 0.4f, 0.05f, 0f, false),
        (VfxId.PlayerDrained, "CFXR2 Poison Cloud", 0.2f, 1f, 2f, false),
        (VfxId.PlayerRespawn, "CFXR Magic Poof", 0.3f, 0.5f, 1.5f, false),
        (VfxId.DogEat, "CFXR3 Hit Misc A", 0.15f, 0.2f, 1f, false),
        (VfxId.DogPet, "CFXR3 _WOW_", 0.5f, 1f, 1.2f, false),
        (VfxId.DogGift, "CFXR2 Shiny Item (Loop)", 0.3f, 0.2f, 2f, false),
        (VfxId.DogRebel, "CFXR2 Cartoon Fight (Loop)", 0.25f, 0.3f, 2f, false),
        (VfxId.DogMoodWorse, "CFXR2 Broken Heart", 0.2f, 1.1f, 2f, false),
        (VfxId.BowlFill, "CFXR3 Hit Misc A", 0.15f, 0.2f, 1f, false),
        (VfxId.MailCollect, "CFXR2 Shiny Item (Loop)", 0.3f, 1f, 1.5f, false),
        (VfxId.Victory, "CFXR4 Firework 1 Cyan-Purple (HDR)", 1.2f, 3f, 4f, false),
        (VfxId.PoleImpact, "CFXR2 Ground Hit", 0.6f, 0.1f, 1f, false),
        (VfxId.PuddleElectrified, "CFXR Electrified 3", 0.5f, 0.15f, 6f, false),
    };

    [MenuItem("Tools/NailIt/Build VFX Library")]
    public static void Build()
    {
        Directory.CreateDirectory("Assets/Resources");

        VfxLibrary library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<VfxLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        Material overlay = AssetDatabase.LoadAssetAtPath<Material>(OverlayPath);
        if (overlay == null)
        {
            overlay = new Material(Shader.Find("NailIt/TileWetOverlay"));
            AssetDatabase.CreateAsset(overlay, OverlayPath);
        }

        library.wetOverlayMaterial = overlay;
        library.entries.Clear();
        foreach (var row in Table)
        {
            string guid = AssetDatabase.FindAssets(row.prefab + " t:Prefab", SearchFolders)
                .FirstOrDefault(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == row.prefab);
            if (guid == null) Debug.LogError($"VfxLibrarySetup: {row.prefab} was not found.");

            library.entries.Add(new VfxLibrary.Entry
            {
                id = row.id,
                prefab = guid != null ? AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)) : null,
                scale = row.scale,
                offset = Vector3.up * row.height,
                lifetime = row.lifetime,
                hideSmoke = row.hideSmoke,
                tint = row.id == VfxId.FireBurn || row.id == VfxId.PlayerBurned ? new Color(1f, 0.28f, 0.04f, 1f) : new Color(1f, 1f, 1f, 0f),
            });
        }

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log($"VfxLibrarySetup: {library.entries.Count} effects.");
    }
}
