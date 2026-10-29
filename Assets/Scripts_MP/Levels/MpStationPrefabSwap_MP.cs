using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MpStationPrefabSwap_MP : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MpStationPrefabSwap_MP>(FindObjectsInactive.Include) != null) return;
        new GameObject("MpStationPrefabSwap_MP").AddComponent<MpStationPrefabSwap_MP>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        Swap(SceneManager.GetActiveScene());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Swap(scene);
    }

    private static void Swap(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        if (!NetworkServer.active && !NetworkClient.active) return;

        int swapped = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null) continue;

            foreach (Cutter cutter in root.GetComponentsInChildren<Cutter>(true))
            {
                swapped += SwapField(cutter, "woodProduct");
                swapped += SwapField(cutter, "brickProduct");
                swapped += SwapField(cutter, "glassProduct");
            }

            foreach (Mixer mixer in root.GetComponentsInChildren<Mixer>(true))
            {
                MixerFormulaSO[] formulas = MpSharedCompat.MpGetField<MixerFormulaSO[]>(mixer, "formulas");
                if (formulas == null) continue;

                foreach (MixerFormulaSO formula in formulas)
                {
                    if (formula == null) continue;
                    swapped += SwapField(formula, "productPrefab");
                }
            }

            foreach (Shop shop in root.GetComponentsInChildren<Shop>(true))
                swapped += SwapField(shop, "objectPrefab");

            foreach (ShopIndex index in root.GetComponentsInChildren<ShopIndex>(true))
                swapped += SwapField(index, "itemPrefab");

            foreach (CarController_Prototype traffic in root.GetComponentsInChildren<CarController_Prototype>(true))
                swapped += SwapField(traffic, "activeCarPrefab");
        }

        if (swapped > 0)
        {
            InteractionDebug.Log($"[MP] {swapped} istasyon urun prefabi MP varyantina cevrildi " +
                                 $"('{scene.name}') - boylece instantiate edilen urunler agli dogar.");
        }
    }

    private static int SwapField(object target, string fieldName)
    {
        if (target == null) return 0;

        GameObject current = MpSharedCompat.MpGetField<GameObject>(target, fieldName);
        if (current == null) return 0;

        GameObject resolved = MpPrefabResolver.Resolve(current);
        if (resolved == null || resolved == current) return 0;

        MpSharedCompat.MpSetField(target, fieldName, resolved);
        return 1;
    }
}
