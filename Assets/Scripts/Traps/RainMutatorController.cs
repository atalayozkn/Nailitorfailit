using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Optional rain mutator (off by default, per level): while it rains a pool of puddles grows at random outdoor spots,
// mud on terrain and slick puddles on roads and sidewalks. A falling grid pole can electrify nearby slick puddles.
public class RainMutatorController : MonoBehaviour
{
    [Header("Mutator")]
    [SerializeField] private bool mutatorEnabled = false;

    [Header("References")]
    [SerializeField] private RainController_Prototype rainController;
    [SerializeField] private Material puddleMaterial;
    [Tooltip("Optional. A plain cylinder is used until there is final art. The pivot should be at the base.")]
    [SerializeField] private GameObject gridPolePrefab;

    [Header("Rain Timing")]
    [SerializeField] private float firstRainDelay = 30f;
    [SerializeField] private float minRainDuration = 45f;
    [SerializeField] private float minDryInterval = 40f;
    [SerializeField] private float maxDryInterval = 80f;

    [Header("Puddles")]
    [SerializeField] private int poolSize = 60;
    [SerializeField] private float spawnInterval = 0.5f;
    [SerializeField] private float minPuddleSize = 1.5f;
    [SerializeField] private float maxPuddleSize = 3.5f;
    [Range(0f, 1f)][SerializeField] private float nearPlayerChance = 0.6f;
    [SerializeField] private float nearPlayerRadius = 25f;
    [Tooltip("Where puddles can appear. Leave the size at zero to use the active terrain.")]
    [SerializeField] private Bounds customArea;

    [Header("Surface Colors")]
    [SerializeField] private Color slickColor = new Color(0.25f, 0.35f, 0.45f, 0.7f);
    [SerializeField] private Color mudColor = new Color(0.25f, 0.17f, 0.1f, 0.75f);
    [SerializeField] private Color electrifiedColor = new Color(0.55f, 0.9f, 1f, 0.9f);

    [Header("Mud")]
    [SerializeField] private float mudSpeedMultiplier = 0.6f;

    [Header("Falling Grid Pole")]
    [SerializeField] private float minPoleInterval = 20f;
    [SerializeField] private float maxPoleInterval = 40f;
    [SerializeField] private float poleSearchRadius = 30f;
    [SerializeField] private float poleFallDuration = 1.5f;
    [SerializeField] private float poleLength = 6f;
    [SerializeField] private float electrifyRadius = 8f;
    [SerializeField] private float electrifyDuration = 6f;

    private const int MaxSpotAttempts = 8;
    private const float SurfaceOffset = 0.03f;
    private const float RayStartHeight = 60f;

    private readonly List<RainPuddle> pool = new();
    private PlayerStateMachine player;
    private Material slickMaterial;
    private Material mudMaterial;
    private Material electrifiedMaterial;
    private Texture2D puddleTexture;
    private Bounds area;
    private int spotMask;
    private int groundLayer;
    private int mudContacts;

    private void Start()
    {
        if (!mutatorEnabled || NetworkModeHelper.IsNetworked) return;

        if (puddleMaterial == null)
        {
            Debug.LogWarning("RainMutatorController: Puddle material is missing, rain mutator is off.", this);
            return;
        }

        if (!TryResolveArea())
        {
            Debug.LogWarning("RainMutatorController: No terrain or custom area found, rain mutator is off.", this);
            return;
        }

        player = FindFirstObjectByType<PlayerStateMachine>();
        groundLayer = LayerMask.NameToLayer("Ground");
        spotMask = LayerMask.GetMask("Default", "Ground", "Interaction");

        CreateMaterials();
        CreatePool();
        StartCoroutine(RainCycleRoutine());
    }
    private void OnDestroy()
    {
        if (puddleTexture != null) Destroy(puddleTexture);
        if (slickMaterial != null) Destroy(slickMaterial);
        if (mudMaterial != null) Destroy(mudMaterial);
        if (electrifiedMaterial != null) Destroy(electrifiedMaterial);
    }

    #region Rain Cycle
    private IEnumerator RainCycleRoutine()
    {
        yield return new WaitForSeconds(firstRainDelay);

        while (true)
        {
            if (rainController != null) rainController.StartRainProcess();

            float elapsed = 0f;
            float nextSpawn = 0f;
            float nextPole = Random.Range(minPoleInterval, maxPoleInterval);

            while (elapsed < minRainDuration || (rainController != null && rainController.IsRaining))
            {
                elapsed += Time.deltaTime;

                if (elapsed >= nextSpawn)
                {
                    nextSpawn = elapsed + spawnInterval;
                    SpawnPuddle();
                }

                if (elapsed >= nextPole)
                {
                    nextPole = elapsed + Random.Range(minPoleInterval, maxPoleInterval);
                    StartCoroutine(GridPoleRoutine());
                }

                yield return null;
            }

            foreach (RainPuddle puddle in pool)
            {
                if (puddle.gameObject.activeSelf) puddle.Dry();
            }

            yield return new WaitForSeconds(Random.Range(minDryInterval, maxDryInterval));
        }
    }
    #endregion

    #region Puddles
    private void SpawnPuddle()
    {
        RainPuddle puddle = GetFreePuddle();
        if (puddle == null) return;

        for (int i = 0; i < MaxSpotAttempts; i++)
        {
            Vector3 rayStart = GetRandomPoint();
            if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, RayStartHeight * 2f, spotMask, QueryTriggerInteraction.Ignore)) continue;
            if (hit.normal.y < 0.9f) continue;

            bool isMud = hit.collider is TerrainCollider;
            bool isPaved = !isMud && hit.collider.gameObject.layer == groundLayer && !hit.collider.TryGetComponent<TileWetness>(out _);
            if (!isMud && !isPaved) continue;

            Material material = isMud ? mudMaterial : slickMaterial;
            puddle.Place(hit.point + hit.normal * SurfaceOffset, hit.normal, isMud, Random.Range(minPuddleSize, maxPuddleSize), material);
            return;
        }
    }
    private Vector3 GetRandomPoint()
    {
        Vector3 point;

        if (player != null && Random.value < nearPlayerChance)
        {
            Vector2 offset = Random.insideUnitCircle * nearPlayerRadius;
            point = player.transform.position + new Vector3(offset.x, 0f, offset.y);
        }
        else
        {
            point = new Vector3(Random.Range(area.min.x, area.max.x), 0f, Random.Range(area.min.z, area.max.z));
        }

        point.y = area.max.y + RayStartHeight;
        return point;
    }
    private RainPuddle GetFreePuddle()
    {
        foreach (RainPuddle puddle in pool)
        {
            if (!puddle.gameObject.activeSelf) return puddle;
        }
        return null;
    }
    private bool TryResolveArea()
    {
        if (customArea.size.sqrMagnitude > 0f)
        {
            area = customArea;
            return true;
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null) return false;

        area = new Bounds(terrain.GetPosition() + terrain.terrainData.size * 0.5f, terrain.terrainData.size);
        return true;
    }
    private void CreatePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            obj.name = "RainPuddle";
            Destroy(obj.GetComponent<MeshCollider>());
            obj.transform.SetParent(transform);

            // The quad's up side is its local -Z, the trigger volume sits above the ground.
            BoxCollider trigger = obj.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1f, 1f, 0.6f);
            trigger.center = new Vector3(0f, 0f, -0.3f);

            MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            RainPuddle puddle = obj.AddComponent<RainPuddle>();
            puddle.Initialize(this, electrifiedMaterial);
            obj.SetActive(false);
            pool.Add(puddle);
        }
    }
    private void CreateMaterials()
    {
        puddleTexture = CreatePuddleTexture();
        slickMaterial = CreateMaterial(slickColor);
        mudMaterial = CreateMaterial(mudColor);
        electrifiedMaterial = CreateMaterial(electrifiedColor);
    }
    private Material CreateMaterial(Color color)
    {
        Material material = new Material(puddleMaterial);
        material.SetColor("_BaseColor", color);
        material.SetTexture("_BaseMap", puddleTexture);
        return material;
    }
    // Soft edge with noise, so overlapping puddles merge into one wet patch.
    private static Texture2D CreatePuddleTexture()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        float seed = Random.Range(0f, 100f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (size - 1f) * 2f - 1f;
                float v = y / (size - 1f) * 2f - 1f;
                float noise = Mathf.PerlinNoise(seed + u * 2.5f, seed + v * 2.5f) * 0.45f;
                float alpha = Mathf.Clamp01((1f - Mathf.Sqrt(u * u + v * v) - 0.2f + noise) * 4f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }
    #endregion

    #region Mud
    public void AddMudContact(PlayerStateMachine stateMachine)
    {
        mudContacts++;
        stateMachine.movementHandler.SetSurfaceSpeedMultiplier(mudSpeedMultiplier);
    }
    public void RemoveMudContact(PlayerStateMachine stateMachine)
    {
        mudContacts = Mathf.Max(0, mudContacts - 1);
        if (mudContacts == 0) stateMachine.movementHandler.SetSurfaceSpeedMultiplier(1f);
    }
    #endregion

    #region Grid Pole
    private IEnumerator GridPoleRoutine()
    {
        RainPuddle target = FindPoleTarget();
        if (target == null) yield break;

        Vector3 impact = target.transform.position;
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        Vector3 fallDirection = new Vector3(randomDirection.x, 0f, randomDirection.y);
        Vector3 basePoint = impact - fallDirection * poleLength;

        GameObject pole = CreatePole();
        pole.transform.position = basePoint;
        Quaternion upright = Quaternion.identity;
        Quaternion fallen = Quaternion.FromToRotation(Vector3.up, fallDirection);

        // Accelerating fall, the slow start is the player's warning.
        float elapsed = 0f;
        while (elapsed < poleFallDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / poleFallDuration);
            pole.transform.rotation = Quaternion.Slerp(upright, fallen, t * t);
            yield return null;
        }

        List<RainPuddle> electrified = new();
        foreach (RainPuddle puddle in pool)
        {
            if (!puddle.gameObject.activeSelf || puddle.IsMud) continue;
            if ((puddle.transform.position - impact).sqrMagnitude > electrifyRadius * electrifyRadius) continue;

            puddle.SetElectrified(true);
            electrified.Add(puddle);
        }

        yield return new WaitForSeconds(electrifyDuration);

        foreach (RainPuddle puddle in electrified) puddle.SetElectrified(false);
        Destroy(pole);
    }
    private RainPuddle FindPoleTarget()
    {
        List<RainPuddle> candidates = new();
        foreach (RainPuddle puddle in pool)
        {
            if (!puddle.gameObject.activeSelf || puddle.IsMud) continue;
            if (player != null && (puddle.transform.position - player.transform.position).sqrMagnitude > poleSearchRadius * poleSearchRadius) continue;
            candidates.Add(puddle);
        }

        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }
    private GameObject CreatePole()
    {
        GameObject pole;
        if (gridPolePrefab != null)
        {
            pole = Instantiate(gridPolePrefab);
        }
        else
        {
            pole = new GameObject("GridPole_Placeholder");
            GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mesh.transform.SetParent(pole.transform);
            mesh.transform.localPosition = Vector3.up * poleLength * 0.5f;
            mesh.transform.localScale = new Vector3(0.25f, poleLength * 0.5f, 0.25f);
        }

        foreach (Collider col in pole.GetComponentsInChildren<Collider>()) Destroy(col);
        return pole;
    }
    #endregion
}
