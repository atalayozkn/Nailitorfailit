using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using ItemScript;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MpSharedCompat
{
    private const BindingFlags AnyInstance =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private const string MultiplayerMenuScene = "MenuScene_MP";

    private sealed class TimerState
    {
        public bool HasTime;
        public float Remaining;
        public double ServerTime;
    }

    private sealed class ScoreState
    {
        public bool HasScore;
        public int Score;
    }

    private static readonly ConditionalWeakTable<GameTimeManager, TimerState> TimerStates =
        new ConditionalWeakTable<GameTimeManager, TimerState>();

    private static readonly ConditionalWeakTable<ScoreManager, ScoreState> ScoreStates =
        new ConditionalWeakTable<ScoreManager, ScoreState>();

    private static FieldInfo FindField(Type type, string name)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            FieldInfo field = current.GetField(name, AnyInstance | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }

        return null;
    }

    private static T GetField<T>(object target, string name, T fallback = default)
    {
        if (target == null) return fallback;

        FieldInfo field = FindField(target.GetType(), name);
        if (field == null) return fallback;

        object value = field.GetValue(target);
        return value is T typed ? typed : fallback;
    }

    private static void SetField(object target, string name, object value)
    {
        if (target == null) return;

        FieldInfo field = FindField(target.GetType(), name);
        if (field != null) field.SetValue(target, value);
    }

    private static MethodInfo FindMethod(Type type, string name, object[] args)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            MethodInfo[] methods = current.GetMethods(AnyInstance | BindingFlags.DeclaredOnly);

            foreach (MethodInfo method in methods)
            {
                if (!string.Equals(method.Name, name, StringComparison.Ordinal)) continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != args.Length) continue;

                bool matches = true;

                for (int i = 0; i < parameters.Length; i++)
                {
                    if (args[i] == null) continue;
                    if (!parameters[i].ParameterType.IsInstanceOfType(args[i]))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches) return method;
            }
        }

        return null;
    }

    private static object Call(object target, string name, params object[] args)
    {
        if (target == null) return null;

        MethodInfo method = FindMethod(target.GetType(), name, args);
        if (method == null)
        {
            Debug.LogWarning($"[MP-COMPAT] {target.GetType().Name}.{name} was not found.");
            return null;
        }

        return method.Invoke(target, args);
    }

    private static void ConsumeNetworked(CarriableObject_SP carriable, NetworkIdentity requester)
    {
        if (carriable == null) return;

        if (NetworkServer.active && carriable.TryGetComponent(out CarriableObject_MP networked))
            networked.ServerConsumeNetworked(requester);
        else
            carriable.OnConsume();
    }

    public static void MarkConsumedForNetwork(this CarriableObject_SP carriable)
    {
        if (carriable == null) return;
        if (GetField(carriable, "isConsumed", false)) return;

        SetField(carriable, "isConsumed", true);

        ObjectSpawner spawner = GetField<ObjectSpawner>(carriable, "spawnerObject");
        if (spawner != null) spawner.ReduceCounter();

        UnityEvent onConsume = GetField<UnityEvent>(carriable, "onConsumeEvent");
        if (onConsume != null) onConsume.Invoke();

        PlayerInteractionHandler handler = PlayerInteractionResolver.Local();
        if (handler != null) handler.ClearCarriedObject();
    }

    public static float GetNormalized(this InteractionProcessHelper helper)
    {
        if (helper == null) return 0f;

        float maxProcessAmount = GetField(helper, "maxProcessAmount", 0f);
        if (maxProcessAmount <= 0f) return 0f;

        float currentProcess = GetField(helper, "currentProcess", 0f);
        return Mathf.Clamp01(currentProcess / maxProcessAmount);
    }

    public static void ApplyNetworkedProgress(this InteractionProcessHelper helper, float normalized)
    {
        if (helper == null) return;
        if (GetField(helper, "isCompleted", false)) return;

        float maxProcessAmount = GetField(helper, "maxProcessAmount", 0f);
        float currentProcess = Mathf.Clamp01(normalized) * maxProcessAmount;

        SetField(helper, "currentProcess", currentProcess);

        InteractionUIHelper canvasHelper = GetField<InteractionUIHelper>(helper, "canvasHelper");
        if (canvasHelper == null) return;

        canvasHelper.UpdateUI(currentProcess);

        if (currentProcess >= maxProcessAmount) canvasHelper.SetActivity(false);
    }

    public static void ServerInteract(this Generator_Prototype generator, NetworkIdentity requester)
    {
        if (generator == null) return;

        PlayerInteractionHandler handler = PlayerInteractionResolver.ForIdentity(requester);
        SetField(generator, "interactionHandler", handler);
        if (handler == null) return;

        if (handler.GetCurrentCarriableType() != CarriableType.Oil) return;

        CarriableObject_SP carriable = handler.GetCurrentCarriable();
        if (carriable == null) return;

        ConsumeNetworked(carriable, requester);
        generator.GainEnergy(GetField(generator, "perOilGainAmount", 0f));
    }

    public static void ServerInteract(this Generator_SP generator, NetworkIdentity requester)
    {
        if (generator == null) return;

        PlayerInteractionHandler handler = PlayerInteractionResolver.ForIdentity(requester);
        SetField(generator, "interactionHandler", handler);
        if (handler == null) return;

        CarriableObject_SP carriable = handler.GetCurrentCarriable();
        if (carriable == null) return;
        if (carriable.carriableType != CarriableType.Oil) return;

        if (!generator.AddEnergy(GetField(generator, "oilEnergyAmount", 0f))) return;

        ConsumeNetworked(carriable, requester);

        UnityEvent onInteract = GetField<UnityEvent>(generator, "onInteractEvent");
        if (onInteract != null) onInteract.Invoke();
    }

    public static void ServerInteract(this Cutter cutter, NetworkIdentity requester)
    {
        if (cutter == null) return;

        PlayerInteractionHandler handler = PlayerInteractionResolver.ForIdentity(requester);
        SetField(cutter, "interactionHandler", handler);
        if (handler == null) return;

        if (GetField(cutter, "isSpawning", false)) return;

        Generator_Prototype generator = GetField<Generator_Prototype>(cutter, "connectedGenerator");
        float perProcessCost = GetField(cutter, "perProcessCost", 0f);

        if (generator == null)
        {
            Debug.LogError($"Cutter '{cutter.name}': connectedGenerator is not assigned.", cutter);
            return;
        }

        if (!generator.HasEnoughEnergy(perProcessCost)) return;

        if (!GetField(cutter, "isObjectPlaced", false))
        {
            RegisterToStation(cutter, handler, requester);
            return;
        }

        if (handler.IsCarrying()) return;

        generator.ConsumeEnergy(perProcessCost);

        InteractionProcessHelper processHelper = GetField<InteractionProcessHelper>(cutter, "processHelper");
        if (processHelper == null) return;

        processHelper.Process();

        if (processHelper.IsCompleted()) SpawnCutterObjects(cutter, requester);
    }

    private static void RegisterToStation(Cutter cutter, PlayerInteractionHandler handler, NetworkIdentity requester)
    {
        CarriableObject_SP carriable = handler.GetCurrentCarriable();
        if (carriable == null) return;
        if (!carriable.isRawMaterial) return;

        CarriableType[] accepted = GetField<CarriableType[]>(cutter, "acceptedCarriableTypes");
        if (accepted == null || !Array.Exists(accepted, type => type == carriable.carriableType)) return;

        SetField(cutter, "placedObject", carriable);
        carriable.SetOccupied();
        SetField(cutter, "isObjectPlaced", true);

        if (NetworkServer.active && carriable.TryGetComponent(out CarriableObject_MP networked))
        {
            networked.ServerPlaceInStation(cutter.GetComponent<NetworkIdentity>(), requester, false);
        }
        else
        {
            Transform placementTransform = GetField<Transform>(cutter, "placementTransform");
            if (placementTransform != null)
            {
                carriable.transform.SetParent(placementTransform);
                carriable.transform.localPosition = Vector3.zero;
                carriable.transform.localRotation = Quaternion.identity;
            }
        }

        handler.ClearCarriedObject();

        InteractionProcessHelper processHelper = GetField<InteractionProcessHelper>(cutter, "processHelper");
        if (processHelper != null) processHelper.ResetProcess();
    }

    private static void SpawnCutterObjects(Cutter cutter, NetworkIdentity requester)
    {
        if (GetField(cutter, "isSpawning", false)) return;

        CarriableObject_SP placedObject = GetField<CarriableObject_SP>(cutter, "placedObject");

        if (placedObject == null)
        {
            SetField(cutter, "isObjectPlaced", false);
            return;
        }

        SetField(cutter, "isSpawning", true);

        CarriableObject_SP objectToConsume = placedObject;
        CarriableType objectType = placedObject.carriableType;

        SetField(cutter, "placedObject", null);
        SetField(cutter, "isObjectPlaced", false);

        ConsumeNetworked(objectToConsume, requester);

        if (GetField(cutter, "isTutorial", false))
        {
            TutorialObjective objective = GetField<TutorialObjective>(cutter, "objective");
            if (objective != null) objective.CompleteCondition();
        }

        cutter.StartCoroutine(CutterSpawnRoutine(cutter, objectType));
    }

    private static IEnumerator CutterSpawnRoutine(Cutter cutter, CarriableType type)
    {
        GameObject objectToSpawn;

        switch (type)
        {
            case CarriableType.Wood:
                objectToSpawn = GetField<GameObject>(cutter, "woodProduct");
                break;

            case CarriableType.Brick:
                objectToSpawn = GetField<GameObject>(cutter, "brickProduct");
                break;

            case CarriableType.Glass:
                objectToSpawn = GetField<GameObject>(cutter, "glassProduct");
                break;

            default:
                objectToSpawn = null;
                break;
        }

        if (objectToSpawn == null)
        {
            SetField(cutter, "isSpawning", false);
            yield break;
        }

        int spawnCount = GetField(cutter, "spawnCount", 0);
        float spawnDelay = GetField(cutter, "spawnDelay", 0f);
        Transform spawnTransform = GetField<Transform>(cutter, "spawnTransform");
        UnityEvent onSpawn = GetField<UnityEvent>(cutter, "onSpawnEvent");

        Vector3 position = spawnTransform != null ? spawnTransform.position : cutter.transform.position;
        WaitForSeconds wait = new WaitForSeconds(spawnDelay);

        for (int i = 0; i < spawnCount; i++)
        {
            UnityEngine.Object.Instantiate(objectToSpawn, position, Quaternion.identity);
            if (onSpawn != null) onSpawn.Invoke();
            if (i < spawnCount - 1) yield return wait;
        }

        SetField(cutter, "isSpawning", false);
    }

    public static void ServerInteract(this Mixer mixer, NetworkIdentity requester)
    {
        if (mixer == null) return;

        PlayerInteractionHandler handler = PlayerInteractionResolver.ForIdentity(requester);
        SetField(mixer, "interactionHandler", handler);
        if (handler == null) return;

        CarriableObject_SP carriable = handler.GetCurrentCarriable();
        if (carriable == null) return;

        CarriableType carriableType = handler.GetCurrentCarriableType();
        if (!MixerAccepts(mixer, carriableType)) return;

        if (GetField<CarriableObject_SP>(mixer, "firstCarriable") == null)
        {
            SetField(mixer, "firstCarriable", carriable);
            MixerDisplayItem(mixer, 1, carriableType);
        }
        else if (GetField<CarriableObject_SP>(mixer, "secondCarriable") == null)
        {
            SetField(mixer, "secondCarriable", carriable);
            MixerDisplayItem(mixer, 2, carriableType);
        }
        else if (GetField<CarriableObject_SP>(mixer, "thirdCarriable") == null)
        {
            SetField(mixer, "thirdCarriable", carriable);
            MixerDisplayItem(mixer, 3, carriableType);
        }
        else
        {
            return;
        }

        PlaceInMixer(mixer, carriable, requester);
    }

    public static void ServerCompare(this Mixer mixer, NetworkIdentity requester)
    {
        if (mixer == null) return;

        CarriableObject_SP first = GetField<CarriableObject_SP>(mixer, "firstCarriable");
        CarriableObject_SP second = GetField<CarriableObject_SP>(mixer, "secondCarriable");
        CarriableObject_SP third = GetField<CarriableObject_SP>(mixer, "thirdCarriable");

        if (first == null || second == null || third == null)
        {
            DropStoredItems(mixer, requester);
            return;
        }

        MixerFormulaSO[] formulas = GetField<MixerFormulaSO[]>(mixer, "formulas");
        CarriableObject_SP[] storedObjects = { first, second, third };
        MixerFormulaSO matchedFormula = null;

        if (formulas != null)
        {
            foreach (MixerFormulaSO formula in formulas)
            {
                if (formula == null) continue;
                if (!formula.Matches(storedObjects)) continue;

                matchedFormula = formula;
                break;
            }
        }

        if (matchedFormula == null)
        {
            DropStoredItems(mixer, requester);
            return;
        }

        Generator_Prototype generator = GetField<Generator_Prototype>(mixer, "connectedGenerator");
        float processCost = GetField(mixer, "processCost", 0f);

        if (generator == null || !generator.HasEnoughEnergy(processCost)) return;

        DestroyStoredItems(mixer, requester);
        SpawnMixerProduct(mixer, matchedFormula);
    }

    private static bool MixerAccepts(Mixer mixer, CarriableType type)
    {
        CarriableType[] accepted = GetField<CarriableType[]>(mixer, "acceptedCarriableTypes");
        if (accepted == null || accepted.Length == 0) return true;

        foreach (CarriableType acceptedType in accepted)
        {
            if (acceptedType == type) return true;
        }

        return false;
    }

    private static void MixerDisplayItem(Mixer mixer, int index, CarriableType type)
    {
        MixerUIHelper uiHelper = GetField<MixerUIHelper>(mixer, "uiHelper");
        if (uiHelper != null) uiHelper.DisplayItem(index, type);
    }

    private static void PlaceInMixer(Mixer mixer, CarriableObject_SP carriable, NetworkIdentity requester)
    {
        Transform placementTransform = GetField<Transform>(mixer, "placementTransform");

        if (placementTransform != null)
        {
            carriable.transform.SetParent(placementTransform);
            carriable.transform.localPosition = Vector3.zero;
            carriable.transform.localRotation = Quaternion.identity;
        }

        if (NetworkServer.active && carriable.TryGetComponent(out CarriableObject_MP networked))
        {
            networked.ServerPlaceInStation(mixer.GetComponent<NetworkIdentity>(), requester, true);
        }
        else
        {
            carriable.SetVisuals(false);
        }

        PlayerInteractionHandler handler = GetField<PlayerInteractionHandler>(mixer, "interactionHandler");
        if (handler != null) handler.ClearCarriedObject();
    }

    private static void DestroyStoredItems(Mixer mixer, NetworkIdentity requester)
    {
        DestroyMixerCarriable(GetField<CarriableObject_SP>(mixer, "firstCarriable"), requester);
        DestroyMixerCarriable(GetField<CarriableObject_SP>(mixer, "secondCarriable"), requester);
        DestroyMixerCarriable(GetField<CarriableObject_SP>(mixer, "thirdCarriable"), requester);

        SetField(mixer, "firstCarriable", null);
        SetField(mixer, "secondCarriable", null);
        SetField(mixer, "thirdCarriable", null);

        MixerUIHelper uiHelper = GetField<MixerUIHelper>(mixer, "uiHelper");
        if (uiHelper != null) uiHelper.ClearImages();
    }

    private static void DestroyMixerCarriable(CarriableObject_SP carriable, NetworkIdentity requester)
    {
        if (carriable == null) return;

        if (NetworkServer.active && carriable.TryGetComponent(out CarriableObject_MP networked))
        {
            networked.ServerConsumeNetworked(requester);
            return;
        }

        UnityEngine.Object.Destroy(carriable.gameObject);
    }

    private static void DropStoredItems(Mixer mixer, NetworkIdentity requester)
    {
        DropMixerCarriable(GetField<CarriableObject_SP>(mixer, "firstCarriable"));
        DropMixerCarriable(GetField<CarriableObject_SP>(mixer, "secondCarriable"));
        DropMixerCarriable(GetField<CarriableObject_SP>(mixer, "thirdCarriable"));

        SetField(mixer, "firstCarriable", null);
        SetField(mixer, "secondCarriable", null);
        SetField(mixer, "thirdCarriable", null);

        MixerUIHelper uiHelper = GetField<MixerUIHelper>(mixer, "uiHelper");
        if (uiHelper != null) uiHelper.ClearImages();
    }

    private static void DropMixerCarriable(CarriableObject_SP carriable)
    {
        if (carriable == null) return;

        if (NetworkServer.active && carriable.TryGetComponent(out CarriableObject_MP networked))
        {
            networked.ServerDropFromStation();
            return;
        }

        carriable.SetVisuals(true);
        carriable.OnDrop();
    }

    private static void SpawnMixerProduct(Mixer mixer, MixerFormulaSO formula)
    {
        if (formula == null || formula.ProductPrefab == null) return;

        Transform productSpawnPoint = GetField<Transform>(mixer, "productSpawnPoint");
        Vector3 position = productSpawnPoint != null ? productSpawnPoint.position : mixer.transform.position;
        Quaternion rotation = productSpawnPoint != null ? productSpawnPoint.rotation : mixer.transform.rotation;

        UnityEngine.Object.Instantiate(formula.ProductPrefab, position, rotation);
    }

    public static void ServerInteract(this Constructor constructor, NetworkIdentity requester)
    {
        if (constructor == null) return;

        PlayerInteractionHandler handler = PlayerInteractionResolver.ForIdentity(requester);
        SetField(constructor, "interactionHandler", handler);
        if (handler == null) return;

        CarriableObject_SP carriable = handler.GetCurrentCarriable();
        if (carriable == null) return;
        if (carriable.isRawMaterial) return;

        CarriableType carriableType = handler.GetCurrentCarriableType();
        if (carriableType != GetField(constructor, "acceptedType", CarriableType.Wood)) return;

        ConstructForRequester(constructor, handler, requester, carriableType);

        UnityEvent onInteract = GetField<UnityEvent>(constructor, "onInteractEvent");
        if (onInteract != null) onInteract.Invoke();
    }

    private static void ConstructForRequester(Constructor constructor, PlayerInteractionHandler handler,
        NetworkIdentity requester, CarriableType type)
    {
        if (GetField(constructor, "isBuilt", false)) return;
        SetField(constructor, "isBuilt", true);

        if (GetField(constructor, "isTutorial", false))
        {
            TutorialObjective objective = GetField<TutorialObjective>(constructor, "objective");
            if (objective != null) objective.CompleteCondition();
        }

        switch (type)
        {
            case CarriableType.Wood:
                SetActive(constructor, "woodObject", true);
                SetField(constructor, "activeHealthComponent",
                    GetField<ObjectHealth>(constructor, "woodHealth"));
                break;

            case CarriableType.Brick:
                SetActive(constructor, "brickObject", true);
                SetField(constructor, "activeHealthComponent",
                    GetField<ObjectHealth>(constructor, "brickHealth"));
                break;

            case CarriableType.Glass:
                SetField(constructor, "activeHealthComponent",
                    GetField<ObjectHealth>(constructor, "glassHealth"));
                SetActive(constructor, "glassObject", true);
                break;
        }

        SetActive(constructor, "bluePrintObject", false);

        Collider interactionCollider = GetField<Collider>(constructor, "interactionCollider");
        if (interactionCollider != null) interactionCollider.enabled = false;

        CarriableObject_SP carriable = handler.GetCurrentCarriable();
        if (carriable != null) ConsumeNetworked(carriable, requester);

        constructor.Invoke("NotifyInteractionHandler", 0.05f);
    }

    private static void SetActive(Constructor constructor, string fieldName, bool active)
    {
        GameObject target = GetField<GameObject>(constructor, fieldName);
        if (target != null) target.SetActive(active);
    }

    private static void ReportConstructorCompletion(Constructor constructor)
    {
        if (constructor == null) return;

        RoomController room = GetField<RoomController>(constructor, "roomController");

        if (room == null) room = constructor.GetComponentInParent<RoomController>();

        if (room == null)
        {
            RoomController[] rooms = UnityEngine.Object.FindObjectsByType<RoomController>(
                FindObjectsSortMode.None);

            float best = float.MaxValue;

            foreach (RoomController candidate in rooms)
            {
                if (candidate == null) continue;

                float distance = (candidate.transform.position - constructor.transform.position).sqrMagnitude;

                if (distance >= best) continue;

                best = distance;
                room = candidate;
            }
        }

        if (room != null)
        {
            SetField(constructor, "roomController", room);
            room.IncreaseCounter();
            return;
        }

        Debug.LogError($"[Constructor] '{constructor.name}' icin RoomController bulunamadi; " +
                       "oda sayacina islenmedi.", constructor);
    }

    private static void TileServerInteract(MonoBehaviour tile, NetworkIdentity requester)
    {
        if (tile == null) return;
        if (GetField(tile, "currentPhase", ConstructionPhase.Construction) == ConstructionPhase.Complete) return;

        InteractionProcessHelper processHelper = GetField<InteractionProcessHelper>(tile, "processHelper");

        if (processHelper == null)
        {
            Debug.LogError($"{tile.name}: processHelper is not assigned - tile cannot be processed.", tile);
            return;
        }

        PlayerInteractionHandler handler = PlayerInteractionResolver.ForIdentity(requester);
        if (handler != null && handler.IsCarrying()) return;

        processHelper.Process();

        if (processHelper.IsCompleted()) CompleteTileConstruction(tile);
    }

    private static void CompleteTileConstruction(MonoBehaviour tile)
    {
        if (GetField(tile, "isTutorial", false))
        {
            TutorialObjective objective = GetField<TutorialObjective>(tile, "objective");
            if (objective != null) objective.CompleteCondition();
        }

        tile.gameObject.layer = LayerMask.NameToLayer("Ground");
        SetField(tile, "currentPhase", ConstructionPhase.Complete);

        Constructor connectedConstructor = GetField<Constructor>(tile, "connectedConstructor");
        ReportConstructorCompletion(connectedConstructor);
    }

    private static float TileGetProcessNormalized(MonoBehaviour tile)
    {
        InteractionProcessHelper processHelper = GetField<InteractionProcessHelper>(tile, "processHelper");
        return processHelper != null ? processHelper.GetNormalized() : 0f;
    }

    private static void TileApplyNetworkedProgress(MonoBehaviour tile, float normalized)
    {
        if (tile == null) return;
        if (GetField(tile, "currentPhase", ConstructionPhase.Construction) == ConstructionPhase.Complete) return;

        InteractionProcessHelper processHelper = GetField<InteractionProcessHelper>(tile, "processHelper");
        if (processHelper != null) processHelper.ApplyNetworkedProgress(normalized);
    }

    private static void TileApplyCompletedState(MonoBehaviour tile)
    {
        if (tile == null) return;
        if (GetField(tile, "currentPhase", ConstructionPhase.Construction) == ConstructionPhase.Complete) return;

        SetField(tile, "currentPhase", ConstructionPhase.Complete);
        tile.gameObject.layer = LayerMask.NameToLayer("Ground");
    }

    public static void ServerInteract(this WoodTile tile, NetworkIdentity requester) =>
        TileServerInteract(tile, requester);

    public static float GetProcessNormalized(this WoodTile tile) => TileGetProcessNormalized(tile);

    public static void ApplyNetworkedProgress(this WoodTile tile, float normalized) =>
        TileApplyNetworkedProgress(tile, normalized);

    public static void ApplyCompletedState(this WoodTile tile) => TileApplyCompletedState(tile);

    public static void ServerInteract(this GlassTile tile, NetworkIdentity requester) =>
        TileServerInteract(tile, requester);

    public static float GetProcessNormalized(this GlassTile tile) => TileGetProcessNormalized(tile);

    public static void ApplyNetworkedProgress(this GlassTile tile, float normalized) =>
        TileApplyNetworkedProgress(tile, normalized);

    public static void ApplyCompletedState(this GlassTile tile) => TileApplyCompletedState(tile);

    public static void ServerInteract(this BrickTile tile, NetworkIdentity requester) =>
        TileServerInteract(tile, requester);

    public static float GetProcessNormalized(this BrickTile tile) => TileGetProcessNormalized(tile);

    public static void ApplyNetworkedProgress(this BrickTile tile, float normalized) =>
        TileApplyNetworkedProgress(tile, normalized);

    public static void ApplyCompletedState(this BrickTile tile) => TileApplyCompletedState(tile);

    public static void ServerInteract(this Shop shop, NetworkIdentity requester)
    {
        if (shop == null) return;

        GameObject objectPrefab = GetField<GameObject>(shop, "objectPrefab");

        if (objectPrefab == null)
        {
            Debug.LogError($"Shop '{shop.name}' has no objectPrefab assigned - nothing to sell.", shop);
            return;
        }

        PlayerInteractionHandler handler = PlayerInteractionResolver.ForIdentity(requester);
        if (handler != null && handler.IsCarrying()) return;

        int cost = GetField(shop, "cost", 0);

        if (CurrencyManager.Instance != null && !CurrencyManager.Instance.HasEnoughCurrency(cost)) return;

        Transform spawnTransform = GetField<Transform>(shop, "spawnTransform");
        Vector3 position = spawnTransform != null ? spawnTransform.position : shop.transform.position;
        Quaternion rotation = spawnTransform != null ? spawnTransform.rotation : shop.transform.rotation;

        GameObject obj = UnityEngine.Object.Instantiate(objectPrefab, position, rotation);

        if (NetworkServer.active && obj.TryGetComponent<NetworkIdentity>(out _))
        {
            NetworkServer.Spawn(obj);

            if (obj.TryGetComponent(out CarriableObject_MP carriableMp))
                carriableMp.ServerGiveTo(requester);
        }
        else if (obj.TryGetComponent(out CarriableObject_SP carriable))
        {
            carriable.SnapToRightHand();
        }

        if (CurrencyManager_MP.Instance != null) CurrencyManager_MP.Instance.RequestSpend(cost);

        UnityEvent onPurchase = GetField<UnityEvent>(shop, "onPurchaseEvent");
        if (onPurchase != null) onPurchase.Invoke();
    }

    public static void ServerBuy(this ShopIndex shopIndex)
    {
        if (shopIndex == null) return;

        int itemCost = GetField(shopIndex, "itemCost", 0);

        if (CurrencyManager.Instance != null && !CurrencyManager.Instance.HasEnoughCurrency(itemCost)) return;

        GameObject itemPrefab = GetField<GameObject>(shopIndex, "itemPrefab");
        if (itemPrefab == null) return;

        Transform spawnPosition = GetField<Transform>(shopIndex, "spawnPosition");
        Vector3 position = spawnPosition != null ? spawnPosition.position : shopIndex.transform.position;
        Quaternion rotation = spawnPosition != null ? spawnPosition.rotation : shopIndex.transform.rotation;

        GameObject obj = UnityEngine.Object.Instantiate(itemPrefab, position, rotation);
        obj.transform.SetParent(null);

        if (CurrencyManager_MP.Instance != null) CurrencyManager_MP.Instance.RequestSpend(itemCost);
    }

    public static void SpawnForRemote(this ObjectSpawner spawner, bool requesterCarrying)
    {
        if (spawner == null) return;

        if (GetField(spawner, "isTutorial", false))
        {
            TutorialObjective objective = GetField<TutorialObjective>(spawner, "objective");
            if (objective != null) objective.CompleteCondition();
        }

        if (requesterCarrying) return;
        if (GetField(spawner, "isOnCooldown", false)) return;

        SetField(spawner, "isOnCooldown", true);

        UnityEvent onInteract = GetField<UnityEvent>(spawner, "onInteractEvent");
        if (onInteract != null) onInteract.Invoke();

        Call(spawner, "SpawnObject");

        spawner.Invoke("ReverseCooldown", GetField(spawner, "interactCooldown", 0f));
    }

    public static bool IsTimerActive(this GameTimeManager manager)
    {
        return manager != null && GetField(manager, "isActive", false);
    }

    public static void ApplyNetworkedTime(this GameTimeManager manager, float remaining, double serverTime,
        bool active)
    {
        if (manager == null) return;

        TimerState state = TimerStates.GetOrCreateValue(manager);
        state.HasTime = true;
        state.Remaining = remaining;
        state.ServerTime = serverTime;

        float currentTime = RemainingFromNetwork(state);

        SetField(manager, "currentTime", currentTime);
        SetField(manager, "isActive", active);

        Call(manager, "UpdateUI");

        if (currentTime <= 0f && active)
        {
            SetField(manager, "isActive", false);
            Call(manager, "NotifyGameEnd");
        }
    }

    private static float RemainingFromNetwork(TimerState state)
    {
        if (!state.HasTime) return 0f;

        float elapsed = (float)(NetworkTime.time - state.ServerTime);
        return Mathf.Max(0f, state.Remaining - elapsed);
    }

    public static void ApplyNetworkedScore(this ScoreManager manager, int score)
    {
        if (manager == null) return;

        ScoreState state = ScoreStates.GetOrCreateValue(manager);
        state.HasScore = true;
        state.Score = score;
    }

    public static int GetNetworkedScore(this ScoreManager manager)
    {
        if (manager == null) return 0;

        return ScoreStates.TryGetValue(manager, out ScoreState state) && state.HasScore ? state.Score : 0;
    }

    public static void ApplyLocalGain(this CurrencyManager manager, int amount)
    {
        if (manager == null) return;

        manager.GainCurrency(amount);
        RefreshLocalCurrencyText(manager);
    }

    public static void ApplyLocalSpend(this CurrencyManager manager, int amount)
    {
        if (manager == null) return;

        manager.SpendCurrency(amount);
        RefreshLocalCurrencyText(manager);
    }

    public static int GetCurrency(this CurrencyManager manager)
    {
        return manager == null ? 0 : GetField(manager, "currentValue", 0);
    }

    /// <summary>
    /// Value of GameManager's serialized startCurrencyAmount - the amount SP hands the
    /// player when a level starts. MP matches use it as their starting wallet too.
    /// </summary>
    public static int GetStartCurrencyAmount(this GameManager gameManager)
    {
        return gameManager == null ? 0 : GetField(gameManager, "startCurrencyAmount", 0);
    }

    /// <summary>
    /// True when the level's authored SP money object is the one being displayed.
    /// </summary>
    public static bool HasAuthoredMoneyHud { get; private set; }

    /// <summary>
    /// True when the level's authored SP timer object is the one being displayed.
    /// </summary>
    public static bool HasAuthoredTimeHud { get; private set; }

    /// <summary>
    /// Overwrites the values of the HUD objects the SP level already contains: the SP
    /// managers are pointed at those same widgets (the private fields singleplayer
    /// scenes assign in the Inspector), so CurrencyManager and GameTimeManager keep
    /// writing the synchronized money/time into the authored objects.
    /// Null clears the binding when the level has no authored HUD.
    /// </summary>
    public static void BindAuthoredPlayerHud(CurrencyText currencyText, TextMeshProUGUI timerText)
    {
        HasAuthoredMoneyHud = currencyText != null && CurrencyManager.Instance != null;
        HasAuthoredTimeHud = timerText != null && GameTimeManager.Instance != null;

        if (HasAuthoredMoneyHud)
        {
            SetField(CurrencyManager.Instance, "currencyText", currencyText);
            Call(CurrencyManager.Instance, "UpdateUI");
        }

        if (HasAuthoredTimeHud)
        {
            SetField(GameTimeManager.Instance, "timerText", timerText);
            Call(GameTimeManager.Instance, "UpdateUI");
        }
    }

    /// <summary>
    /// Points the MP player's PauseMenu at its own PlayerStateMachine. The prefab leaves
    /// that slot empty and PauseMenu.Awake fills it with FindFirstObjectByType, which
    /// can return a REMOTE player's state machine (or the authored SP one), so the
    /// cursor would be opened on the wrong avatar.
    /// </summary>
    public static void BindPauseStateMachine(PauseMenu pauseMenu, PlayerStateMachine stateMachine)
    {
        if (pauseMenu == null || stateMachine == null) return;

        SetField(pauseMenu, "stateMachine", stateMachine);
    }

    /// <summary>
    /// CurrencyManager caches the first CurrencyText it finds when a scene loads, and in
    /// an MP level that is the authored SP player's HUD. When that authored HUD is the
    /// visible one (bound above) there is nothing to refresh here; otherwise the HUD
    /// lives on the Mirror player prefab and has to be refreshed explicitly.
    /// </summary>
    public static void RefreshLocalCurrencyText(this CurrencyManager manager)
    {
        if (manager == null) return;
        if (HasAuthoredMoneyHud) return;

        NetworkIdentity localPlayer = NetworkClient.localPlayer;
        if (localPlayer == null) return;

        CurrencyText text = localPlayer.GetComponentInChildren<CurrencyText>(true);
        if (text != null) text.UpdateUI(GetField(manager, "currentValue", 0));
    }

    public static void LoadMultiplayerMenu(this GameSceneManager manager)
    {
        if (manager == null) return;

        manager.StartCoroutine(LoadMultiplayerMenuRoutine(manager));
    }

    private static IEnumerator LoadMultiplayerMenuRoutine(GameSceneManager manager)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(MultiplayerMenuScene);

        if (operation == null)
        {
            Debug.LogWarning($"[SCENES] Could not load '{MultiplayerMenuScene}'.");
            yield break;
        }

        while (!operation.isDone) yield return null;

        SetField(manager, "activeScene", MultiplayerMenuScene);

        if (GameManager.Instance != null) GameManager.Instance.OnSceneLoaded();
    }

    public static float GetTimeRewardAmount(this MailBox mailBox)
    {
        return mailBox == null ? 0f : GetField(mailBox, "timeRewardAmount", 0f);
    }

    public static void ApplyMailPresentVisual(this MailBox mailBox, bool present)
    {
        if (mailBox == null) return;
        if (GetField(mailBox, "isMailPresent", false) == present) return;

        SetField(mailBox, "isMailPresent", present);

        if (!present) return;

        UnityEvent onTrigger = GetField<UnityEvent>(mailBox, "onTriggerEvent");
        if (onTrigger != null) onTrigger.Invoke();
    }

    public static int GetMaxFood(this FoodBowl bowl)
    {
        return bowl == null ? 0 : GetField(bowl, "maxFood", 0);
    }

    public static int GetCurrentFood(this FoodBowl bowl)
    {
        return bowl == null ? 0 : GetField(bowl, "currentFood", 0);
    }

    public static int GetPerFillAmount(this FoodBowl bowl)
    {
        return bowl == null ? 0 : GetField(bowl, "perFillAmount", 0);
    }

    public static void ApplyNetworkedFood(this FoodBowl bowl, int amount)
    {
        if (bowl == null) return;

        int maxFood = GetField(bowl, "maxFood", 0);
        int currentFood = Mathf.Clamp(amount, 0, maxFood);

        SetField(bowl, "currentFood", currentFood);

        Slider slider = GetField<Slider>(bowl, "slider");
        if (slider != null) slider.value = currentFood;
    }
}
