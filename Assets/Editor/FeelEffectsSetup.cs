using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoreMountains.Feedbacks;
using MoreMountains.FeedbacksForThirdParty;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-click wiring for the playtest feel requests:
// generator shake + smoke, car variants with fitted colliders / exhaust / mesh-only vibration,
// car spawn heights in Levels 1-5, and player foot dust + Feel landing shake.
// Every step is safe to run again.
public static class FeelEffectsSetup
{
    private const string GeneratorPrefab = "Assets/Prefabs/Helpers/Generator/Generator.prefab";
    private const string GeneratorPrototypePrefab = "Assets/Prefabs/Helpers/Generator/Generator_Prototype.prefab";
    private const string CarFolder = "Assets/Prefabs/Props/Cars";
    private const string BaseCarPrefab = CarFolder + "/Car_Active.prefab";
    private const string PlayerPrefab = "Assets/Prefabs/Player/Player_Package_Default.prefab";
    private const string CarModelFolder = "Assets/AA İNDİRİLMİŞ PAKETLER/ZZZ ARABALAR";
    private const string SmokeMaterial =
        "Assets/Plugins/cartoon fx/Cartoon FX Remaster/CFXR Assets/Graphics/cfxr smoke cloud x4 ab blurred.mat";

    private static readonly (string variant, string model)[] CarVariants =
    {
        ("Car_Active_Hatchback", "Hatchback Car_15"),
        ("Car_Active_PickUp", "Pick Up_11"),
        ("Car_Active_Van", "N Van_10"),
        ("Car_Active_Sport", "Sport Car_39"),
        ("Car_Active_Blue", "car_1_blue"),
    };

    private static readonly string[] Levels =
    {
        "Assets/Scenes/Level_01.unity",
        "Assets/Scenes/Level_02.unity",
        "Assets/Scenes/Level_03.unity",
        "Assets/Scenes/Level_04.unity",
        "Assets/Scenes/Level_05.unity",
    };

    private const string VisualName = "Visual";
    private const float FrontKillDepth = 0.6f;   // matches the old crash trigger depth
    private const float ColliderLift = 0.05f;    // collider bottom sits above the wheels, never scrapes the road
    private const float SpawnClearance = 0.03f;

    [MenuItem("Tools/NailIt/Feel/Run All Feel Setup")]
    public static void RunAll()
    {
        SetUpGenerator();
        BuildCars();
        SetUpPlayer();
        FixCarSpawnsInLevels();
    }

    // ---------------------------------------------------------------- Generator

    [MenuItem("Tools/NailIt/Feel/1 Generator Shake And Smoke")]
    public static void SetUpGenerator()
    {
        // Generator.prefab (Generator_SP) is only in test scenes; Levels 1-5 use Generator_Prototype
        // nested in the Mixer and WoodCutter stations, so both get the effects.
        SetUpGeneratorPrefab(GeneratorPrefab);
        SetUpGeneratorPrefab(GeneratorPrototypePrefab);
    }

    private static void SetUpGeneratorPrefab(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform mesh = root.transform.Find("Mesh");
            if (mesh == null)
            {
                Debug.LogError($"[Feel] {prefabPath} has no 'Mesh' child.");
                return;
            }

            // Static-batched meshes cannot move at runtime, so the shaken mesh must not be static.
            foreach (Transform t in mesh.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);

            // The shaken mesh must not carry the collider: move it to a still sibling.
            BoxCollider meshCollider = mesh.GetComponent<BoxCollider>();
            if (meshCollider != null)
            {
                Transform holder = root.transform.Find("Mesh_Collider");
                if (holder == null)
                {
                    holder = new GameObject("Mesh_Collider").transform;
                    holder.SetParent(root.transform, false);
                }
                holder.localPosition = mesh.localPosition;
                holder.localRotation = mesh.localRotation;
                holder.localScale = mesh.localScale;
                holder.gameObject.layer = mesh.gameObject.layer;
                holder.gameObject.tag = mesh.gameObject.tag;

                if (!holder.TryGetComponent(out BoxCollider copy)) copy = holder.gameObject.AddComponent<BoxCollider>();
                EditorUtility.CopySerialized(meshCollider, copy);
                Object.DestroyImmediate(meshCollider);
            }

            Bounds b = LocalRendererBounds(root.transform, mesh);
            Vector3 smokePos = new Vector3(b.center.x, b.max.y, b.center.z);
            ParticleSystem smoke = GetOrCreateParticle(root.transform, "Generator_Smoke", smokePos,
                Quaternion.Euler(-90f, 0f, 0f));
            ConfigureSmoke(smoke, new Color(0.32f, 0.32f, 0.32f, 0.5f),
                lifetime: new Vector2(1.2f, 1.8f), speed: new Vector2(0.3f, 0.55f), size: new Vector2(0.12f, 0.25f),
                rate: 5f, coneAngle: 10f, radius: 0.04f, grow: 2.4f, rise: -0.04f, playOnAwake: false);

            VisualShaker shaker = GetOrAdd<VisualShaker>(root);
            SetFields(shaker, so =>
            {
                so.FindProperty("target").objectReferenceValue = mesh;
                so.FindProperty("positionAmplitude").floatValue = 0.008f;
                so.FindProperty("rotationAmplitude").floatValue = 0.45f;
                so.FindProperty("frequency").floatValue = 28f;
                so.FindProperty("shakeOnEnable").boolValue = false;
                so.FindProperty("speedSource").objectReferenceValue = null;
            });

            GeneratorRunningFX fx = GetOrAdd<GeneratorRunningFX>(root);
            SetFields(fx, so =>
            {
                so.FindProperty("generator").objectReferenceValue = root.GetComponent<Generator_SP>();
                so.FindProperty("prototypeGenerator").objectReferenceValue = root.GetComponent<Generator_Prototype>();
                so.FindProperty("shaker").objectReferenceValue = shaker;
                so.FindProperty("smoke").objectReferenceValue = smoke;
            });

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[Feel] {root.name}: shake on 'Mesh' (static flags cleared), smoke added.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------------------------------------------------------- Cars

    [MenuItem("Tools/NailIt/Feel/2 Build Car Variants")]
    public static void BuildCars()
    {
        // Base car first: its wheel line and width are the reference for the others.
        CarReference reference = ProcessCar(BaseCarPrefab, null, null);
        if (reference == null) return;

        foreach (var (variant, model) in CarVariants)
        {
            string modelPath = $"{CarModelFolder}/{model}.prefab";
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
            {
                Debug.LogError($"[Feel] Car model missing: {modelPath}");
                continue;
            }

            string path = $"{CarFolder}/{variant}.prefab";
            if (!File.Exists(path)) AssetDatabase.CopyAsset(BaseCarPrefab, path);
            ProcessCar(path, modelAsset, reference);
        }

        AssetDatabase.SaveAssets();
    }

    // Builds one car variant from any model, kept out of normal traffic (used by hazard setups).
    public static bool BuildStandaloneVariant(string variantPath, GameObject model)
    {
        CarReference reference = ProcessCar(BaseCarPrefab, null, null);
        if (reference == null || model == null) return false;

        if (!File.Exists(variantPath)) AssetDatabase.CopyAsset(BaseCarPrefab, variantPath);
        return ProcessCar(variantPath, model, reference) != null;
    }

    private class CarReference
    {
        public float Bottom;   // wheel line, local Y
        public float Width;
        public float CenterZ;
        public float MockFrontGap;
        public float MockY;
    }

    private static CarReference ProcessCar(string prefabPath, GameObject model, CarReference reference)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Car_Active car = root.GetComponent<Car_Active>();
            if (car == null)
            {
                Debug.LogError($"[Feel] {prefabPath} has no Car_Active.");
                return null;
            }

            GameObject mock = new SerializedObject(car).FindProperty("mockPlayerObject").objectReferenceValue as GameObject;
            Transform visual = EnsureVisual(root.transform, mock);

            if (model != null)
            {
                for (int i = visual.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(visual.GetChild(i).gameObject);

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, visual);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                StripToVisualOnly(instance);
                FaceForward(instance.transform, visual);

                // Same width as the base car so it fits the lanes, then put the wheels on the base wheel line.
                Bounds b0 = LocalRendererBounds(root.transform, visual);
                if (b0.size.x > 0.001f) instance.transform.localScale *= reference.Width / b0.size.x;
                Bounds b1 = LocalRendererBounds(root.transform, visual);
                instance.transform.localPosition += new Vector3(-b1.center.x, reference.Bottom - b1.min.y,
                    reference.CenterZ - b1.center.z);
            }
            else
            {
                StripToVisualOnly(visual.gameObject);
            }

            Bounds bounds = LocalRendererBounds(root.transform, visual);
            CarReference result = reference ?? new CarReference
            {
                Bottom = bounds.min.y,
                Width = bounds.size.x,
                CenterZ = bounds.center.z,
                MockFrontGap = mock != null ? bounds.max.z - mock.transform.localPosition.z : 0f,
                MockY = mock != null ? mock.transform.localPosition.y : 0f,
            };

            FitCarColliders(root, bounds, prefabPath);

            if (mock != null && model != null)
            {
                Vector3 p = mock.transform.localPosition;
                mock.transform.localPosition = new Vector3(p.x, result.MockY, bounds.max.z - result.MockFrontGap);
            }

            // Mesh-only vibration: Visual holds renderers, colliders stay on the root.
            VisualShaker shaker = GetOrAdd<VisualShaker>(root);
            SetFields(shaker, so =>
            {
                so.FindProperty("target").objectReferenceValue = visual;
                so.FindProperty("positionAmplitude").floatValue = 0.012f;
                so.FindProperty("rotationAmplitude").floatValue = 0.6f;
                so.FindProperty("frequency").floatValue = 18f;
                so.FindProperty("shakeOnEnable").boolValue = true;
                so.FindProperty("speedSource").objectReferenceValue = root.GetComponent<Rigidbody>();
                so.FindProperty("fullShakeSpeed").floatValue = 10f;
            });

            Vector3 exhaustPos = new Vector3(bounds.size.x * 0.28f, bounds.min.y + 0.22f, bounds.min.z - 0.02f);
            ParticleSystem exhaust = GetOrCreateParticle(root.transform, "Exhaust", exhaustPos,
                Quaternion.LookRotation(Vector3.back));
            ConfigureSmoke(exhaust, new Color(0.62f, 0.62f, 0.62f, 0.45f),
                lifetime: new Vector2(0.6f, 1.0f), speed: new Vector2(0.4f, 0.8f), size: new Vector2(0.14f, 0.26f),
                rate: 16f, coneAngle: 12f, radius: 0.03f, grow: 2.2f, rise: -0.05f, playOnAwake: true);

            CarExhaust exhaustComp = GetOrAdd<CarExhaust>(root);
            SetFields(exhaustComp, so => so.FindProperty("exhaust").objectReferenceValue = exhaust);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[Feel] {Path.GetFileNameWithoutExtension(prefabPath)}: size {bounds.size:F2}, " +
                      $"wheel line y={bounds.min.y:F2}" + (model != null ? $", model '{model.name}'" : ""));
            return result;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Wraps the car model children into one 'Visual' holder (only renderers live there).
    private static Transform EnsureVisual(Transform root, GameObject mock)
    {
        Transform visual = root.Find(VisualName);
        if (visual != null) return visual;

        visual = new GameObject(VisualName).transform;
        visual.SetParent(root, false);
        visual.gameObject.layer = root.gameObject.layer;

        var models = new List<Transform>();
        foreach (Transform child in root)
        {
            if (child == visual) continue;
            if (mock != null && child == mock.transform) continue;
            if (child.GetComponentInChildren<Renderer>(true) == null) continue;
            if (child.GetComponentInChildren<ParticleSystem>(true) != null) continue;
            models.Add(child);
        }

        foreach (Transform m in models) m.SetParent(visual, true);
        return visual;
    }

    // Keeps transforms, meshes and renderers; drops anything with physics or behaviour.
    private static void StripToVisualOnly(GameObject go)
    {
        foreach (var j in go.GetComponentsInChildren<Joint>(true)) Object.DestroyImmediate(j);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var r in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(r);
        foreach (var m in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(m);
        foreach (var a in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
    }

    // Wheel names tell where the front is (… FR / FL / Front_…, … BR / BL / Rear_…).
    private static void FaceForward(Transform model, Transform space)
    {
        var front = new List<Vector3>();
        var rear = new List<Vector3>();

        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.Trim();
            string lower = n.ToLowerInvariant();
            Vector3 p = space.InverseTransformPoint(t.position);

            string[] tokens = n.Split(' ', '_');

            if (tokens.Contains("FR") || tokens.Contains("FL") || lower.StartsWith("front")) front.Add(p);
            else if (tokens.Contains("BR") || tokens.Contains("BL") || lower.StartsWith("rear") || lower.StartsWith("back")) rear.Add(p);
        }

        if (front.Count == 0 || rear.Count == 0)
        {
            Debug.LogWarning($"[Feel] '{model.name}': no wheel names found, assuming it faces +Z. Check it in the prefab.");
            return;
        }

        Vector3 dir = Average(front) - Average(rear);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        float yaw = Vector3.SignedAngle(dir, Vector3.forward, Vector3.up);
        yaw = Mathf.Round(yaw / 90f) * 90f;
        model.localRotation = Quaternion.Euler(0f, yaw, 0f) * model.localRotation;
    }

    // Solid body box + front crash trigger, both fitted to the visible car.
    private static void FitCarColliders(GameObject root, Bounds b, string label)
    {
        BoxCollider[] boxes = root.GetComponents<BoxCollider>();
        BoxCollider trigger = boxes.FirstOrDefault(x => x.isTrigger);
        BoxCollider solid = boxes.FirstOrDefault(x => !x.isTrigger);

        if (solid == null) solid = root.AddComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
        }

        float bottom = b.min.y + ColliderLift;
        float height = b.max.y - bottom;
        float frontStart = b.max.z - FrontKillDepth;

        string before = $"solid c{solid.center:F2} s{solid.size:F2} / trigger c{trigger.center:F2} s{trigger.size:F2}";

        solid.center = new Vector3(b.center.x, bottom + height * 0.5f, (b.min.z + frontStart + 0.05f) * 0.5f);
        solid.size = new Vector3(b.size.x * 0.98f, height, frontStart + 0.05f - b.min.z);

        trigger.center = new Vector3(b.center.x, bottom + height * 0.5f, (frontStart + b.max.z + 0.05f) * 0.5f);
        trigger.size = new Vector3(b.size.x * 0.92f, height, b.max.z + 0.05f - frontStart);

        Debug.Log($"[Feel] {Path.GetFileNameWithoutExtension(label)} colliders refit. Before: {before}. " +
                  $"After: solid c{solid.center:F2} s{solid.size:F2} / trigger c{trigger.center:F2} s{trigger.size:F2}");
    }

    // ---------------------------------------------------------------- Level spawns

    [MenuItem("Tools/NailIt/Feel/3 Fix Car Spawns And Add Variants (Levels 1-5)")]
    public static void FixCarSpawnsInLevels()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        GameObject[] extras = CarVariants
            .Select(v => AssetDatabase.LoadAssetAtPath<GameObject>($"{CarFolder}/{v.variant}.prefab"))
            .Where(g => g != null)
            .ToArray();

        foreach (string level in Levels)
        {
            var scene = EditorSceneManager.OpenScene(level, OpenSceneMode.Single);
            Physics.SyncTransforms();
            int moved = 0, total = 0;

            foreach (var controller in Object.FindObjectsByType<CarController_Prototype>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                var so = new SerializedObject(controller);
                var basePrefab = so.FindProperty("activeCarPrefab").objectReferenceValue as GameObject;
                float bottom = basePrefab != null ? ColliderBottom(basePrefab) : 0f;

                var extraProp = so.FindProperty("extraCarPrefabs");
                extraProp.arraySize = extras.Length;
                for (int i = 0; i < extras.Length; i++)
                    extraProp.GetArrayElementAtIndex(i).objectReferenceValue = extras[i];
                so.ApplyModifiedProperties();

                var points = so.FindProperty("spawnPoints");
                for (int i = 0; i < points.arraySize; i++)
                {
                    var point = points.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                    if (point == null) continue;
                    total++;

                    if (!FindGround(point.position, controller.transform, out RaycastHit hit))
                    {
                        Debug.LogWarning($"[Feel] {scene.name}/{point.name}: no ground under spawn point, left as is.", point);
                        continue;
                    }

                    float targetY = hit.point.y - bottom + SpawnClearance;
                    float delta = targetY - point.position.y;
                    if (Mathf.Abs(delta) < 0.01f) continue;

                    Undo.RecordObject(point, "Fix car spawn height");
                    point.position = new Vector3(point.position.x, targetY, point.position.z);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(point);
                    moved++;
                    Debug.Log($"[Feel] {scene.name}/{point.name}: y {targetY - delta:F2} -> {targetY:F2} " +
                              $"(ground '{hit.collider.name}' at {hit.point.y:F2})", point);
                }

                EditorUtility.SetDirty(controller);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Feel] {scene.name}: {moved}/{total} car spawn points moved, {extras.Length} extra cars added.");
        }
    }

    private static bool FindGround(Vector3 position, Transform ignoreRoot, out RaycastHit best)
    {
        best = default;
        float bestDistance = float.MaxValue;
        RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 3f, Vector3.down, 10f, ~0,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(ignoreRoot)) continue;
            if (hit.collider.CompareTag("Player")) continue;
            Rigidbody body = hit.collider.attachedRigidbody;
            if (body != null && !body.isKinematic) continue;
            if (hit.distance >= bestDistance) continue;

            bestDistance = hit.distance;
            best = hit;
        }

        return bestDistance < float.MaxValue;
    }

    private static float ColliderBottom(GameObject prefab)
    {
        float bottom = float.MaxValue;
        foreach (BoxCollider box in prefab.GetComponentsInChildren<BoxCollider>(true))
        {
            if (box.isTrigger) continue;
            Vector3 world = box.transform.TransformPoint(box.center - Vector3.up * (box.size.y * 0.5f));
            bottom = Mathf.Min(bottom, prefab.transform.InverseTransformPoint(world).y);
        }
        return bottom == float.MaxValue ? 0f : bottom;
    }

    // ---------------------------------------------------------------- Player

    [MenuItem("Tools/NailIt/Feel/4 Player Foot Dust And Landing Shake")]
    public static void SetUpPlayer()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            PlayerMovement movement = root.GetComponentInChildren<PlayerMovement>(true);
            if (movement == null)
            {
                Debug.LogError("[Feel] No PlayerMovement in the player package.");
                return;
            }

            GameObject body = movement.gameObject;
            PlayerStateMachine stateMachine = body.GetComponentInParent<PlayerStateMachine>(true);
            if (stateMachine == null) stateMachine = root.GetComponentInChildren<PlayerStateMachine>(true);

            ParticleSystem dust = GetOrCreateParticle(body.transform, "FootDust", Vector3.up * 0.05f,
                Quaternion.Euler(-90f, 0f, 0f));
            ConfigureDust(dust);

            Transform feedbackHolder = body.transform.Find("LandShake_Feedback");
            if (feedbackHolder == null)
            {
                feedbackHolder = new GameObject("LandShake_Feedback").transform;
                feedbackHolder.SetParent(body.transform, false);
            }

            MMF_Player landPlayer = GetOrAdd<MMF_Player>(feedbackHolder.gameObject);
            MMF_CinemachineImpulse impulse = landPlayer.FeedbacksList != null ? landPlayer.GetFeedbackOfType<MMF_CinemachineImpulse>() : null;
            if (impulse == null)
                impulse = (MMF_CinemachineImpulse)landPlayer.AddFeedback(typeof(MMF_CinemachineImpulse));
            impulse.m_ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            impulse.m_ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            impulse.m_ImpulseDefinition.ImpulseDuration = 0.18f;
            impulse.m_ImpulseDefinition.ImpulseChannel = 1;
            impulse.Velocity = new Vector3(0f, -0.12f, 0f);
            EditorUtility.SetDirty(landPlayer);

            int listeners = 0;
            foreach (CinemachineCamera cam in root.GetComponentsInChildren<CinemachineCamera>(true))
            {
                if (cam.GetComponent<CinemachineImpulseListener>() != null) continue;
                cam.gameObject.AddComponent<CinemachineImpulseListener>();
                listeners++;
            }

            PlayerFootDust footDust = GetOrAdd<PlayerFootDust>(body);
            SetFields(footDust, so =>
            {
                so.FindProperty("movement").objectReferenceValue = movement;
                so.FindProperty("stateMachine").objectReferenceValue = stateMachine;
                so.FindProperty("dust").objectReferenceValue = dust;
                so.FindProperty("landFeedback").objectReferenceValue = landPlayer;
            });

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            Debug.Log($"[Feel] Player: foot dust + landing shake on '{body.name}', {listeners} impulse listener(s) added.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------------------------------------------------------- Particles

    private static ParticleSystem GetOrCreateParticle(Transform parent, string name, Vector3 localPos, Quaternion localRot)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
        }
        t.localPosition = localPos;
        t.localRotation = localRot;
        t.localScale = Vector3.one;
        t.gameObject.layer = parent.gameObject.layer;
        return t.TryGetComponent(out ParticleSystem ps) ? ps : t.gameObject.AddComponent<ParticleSystem>();
    }

    private static void ConfigureSmoke(ParticleSystem ps, Color color, Vector2 lifetime, Vector2 speed, Vector2 size,
        float rate, float coneAngle, float radius, float grow, float rise, bool playOnAwake)
    {
        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = playOnAwake;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = color;
        main.gravityModifier = rise;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 60;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = rate;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = coneAngle;
        shape.radius = radius;

        ApplyCommonLook(ps, grow);
    }

    private const string DustMaterialPath = "Assets/Materials/Dust_Opaque_Unlit.mat";

    // Stylised dust: opaque unlit sphere blobs that pop up and shrink away (no transparency, no lighting).
    private static void ConfigureDust(ParticleSystem ps)
    {
        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
        main.startRotation = 0f;
        main.startColor = new Color(0.62f, 0.55f, 0.45f, 1f);
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 120;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f; // puffs come from Emit() on steps and landing

        // Flat ring at the feet, puffs roll outward and slightly up.
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.15f;
        shape.radiusThickness = 0.3f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        // Loose enough that the landing ring burst travels ~0.8 m before settling.
        // Puff size/speed/alpha per kind are set by PlayerFootDust via EmitParams.
        limit.limit = 0.3f;
        limit.dampen = 0.1f;

        // Opaque, so no alpha fade: grow quickly, then shrink to nothing.
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var shrink = new AnimationCurve(
            new Keyframe(0f, 0.5f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, shrink);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = false;
        var sheet = ps.textureSheetAnimation;
        sheet.enabled = false;

        // Built-in sphere mesh: a solid round blob from any angle.
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh sphere = temp.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(temp);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = sphere;
        renderer.sharedMaterial = GetOrCreateDustMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Material GetOrCreateDustMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(DustMaterialPath);
        if (mat != null) return mat;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("[Feel] URP Particles/Unlit shader not found, dust falls back to the smoke material.");
            return AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterial);
        }

        // Opaque surface, white base: the particle colour (vertex colour) gives the dust tint.
        mat = new Material(shader) { name = "Dust_Opaque_Unlit" };
        mat.SetFloat("_Surface", 0f);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetOverrideTag("RenderType", "Opaque");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        mat.SetInt("_ZWrite", 1);
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        AssetDatabase.CreateAsset(mat, DustMaterialPath);
        return mat;
    }

    private static void ApplyCommonLook(ParticleSystem ps, float grow)
    {
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, grow));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        // The smoke texture is a 2x2 sheet: each particle keeps one random puff.
        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = 2;
        sheet.numTilesY = 2;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);
        sheet.cycleCount = 1;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterial);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (renderer.sharedMaterial == null) Debug.LogWarning($"[Feel] Smoke material missing: {SmokeMaterial}");
    }

    // ---------------------------------------------------------------- Helpers

    private static Bounds LocalRendererBounds(Transform space, Transform under)
    {
        bool has = false;
        Bounds result = default;

        foreach (Renderer r in under.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            if (r.name.StartsWith("__")) continue;

            Bounds local;
            if (r is SkinnedMeshRenderer sk && sk.sharedMesh != null) local = sk.sharedMesh.bounds;
            else
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                local = mf.sharedMesh.bounds;
            }

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = space.InverseTransformPoint(r.transform.TransformPoint(corner));
                if (!has) { result = new Bounds(p, Vector3.zero); has = true; }
                else result.Encapsulate(p);
            }
        }

        return result;
    }

    private static Vector3 Average(List<Vector3> points)
    {
        Vector3 sum = Vector3.zero;
        foreach (Vector3 p in points) sum += p;
        return sum / points.Count;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        return go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();
    }

    private static void SetFields(Object target, System.Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
