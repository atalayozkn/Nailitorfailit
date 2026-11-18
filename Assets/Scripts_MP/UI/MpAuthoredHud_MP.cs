using System;
using System.Linq;
using System.Reflection;
using Mirror;
using PlayerScripts;
using UnityEngine;
using UnityEngine.UI;

// The level scene keeps its authored SP player package active for the HUD (time, money). That package's other HUD
// parts - the new stamina bar and the dog bars - are wired to the authored Player_Default / Dog, which MP disables,
// so they stood still while the MP prefab drew its own (old, side) copies next to them.
// Here the authored HUD is re-pointed to the LOCAL MP player and the MP prefab's duplicates are hidden.
// Levels without an authored package keep the MP prefab's own HUD.
public static class MpAuthoredHud_MP
{
    private const BindingFlags AnyField = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    public static void Bridge(Transform localPackage)
    {
        if (localPackage == null) return;

        Transform authored = FindAuthoredPackage();
        if (authored == null) return;

        int rebound = BridgeStamina(localPackage, authored) + BridgeDog(localPackage, authored);

        // The authored package also carries an emote wheel + listener on the same X key. Left on, X opened that dead
        // copy next to the local player's own wheel (second cursor unlock, emote sent to a disabled player).
        foreach (EmoteListener listener in authored.GetComponentsInChildren<EmoteListener>(true)) listener.enabled = false;
        foreach (EmoteWheel wheel in authored.GetComponentsInChildren<EmoteWheel>(true)) wheel.gameObject.SetActive(false);

        InteractionDebug.Log($"[MP-HUD] authored HUD '{authored.name}' re-pointed to the local player " +
                             $"({rebound} binding(s)); the MP prefab's duplicate bars are hidden.");
    }

    private static Transform FindAuthoredPackage()
    {
        foreach (PlayerStaminaHandler handler in
                 UnityEngine.Object.FindObjectsByType<PlayerStaminaHandler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (handler == null || handler.GetComponentInParent<NetworkIdentity>(true) != null) continue;

            Transform root = handler.transform.root;
            if (root.GetComponentInChildren<StaminaBarVisual>(true) != null) return root;
        }

        return null;
    }

    private static int BridgeStamina(Transform localPackage, Transform authored)
    {
        PlayerStaminaHandler local = localPackage.GetComponentInChildren<PlayerStaminaHandler>(true);
        StaminaBarVisual visual = authored.GetComponentInChildren<StaminaBarVisual>(true);
        Slider authoredSlider = visual != null ? visual.GetComponent<Slider>() : null;
        if (local == null || visual == null || authoredSlider == null) return 0;

        Set(local, "staminaSlider", authoredSlider);
        Set(visual, "stamina", local);
        Watch(visual.gameObject, visual, local);

        authoredSlider.maxValue = local.MaxEnergy;
        authoredSlider.value = local.Energy01 * local.MaxEnergy;

        HideOwn(localPackage, "Stamina_Slider", authored);
        return 2;
    }

    private static int BridgeDog(Transform localPackage, Transform authored)
    {
        DogStateMachine dog = localPackage.GetComponentInChildren<DogStateMachine>(true);
        if (dog == null) return 0;

        int count = 0;
        count += Rebind(dog.GetComponent<SatietyController>(), Slider(authored, "Slider_Satiety"), "maxSatiety", "currentSatiety");
        count += Rebind(dog.GetComponent<FavorController>(), Slider(authored, "Slider_Favor"), "maxFavor", "currentFavor");
        count += Rebind(dog.GetComponent<DogEnergyController>(), Slider(authored, "Slider_Energy"), "maxEnergy", "currentEnergy");

        // The bowl bar (bowl icon) shows how much food is left in the food bowl. The bowl is part of the player's own
        // package and writes to the slider of the package's hidden DogUI, so the authored bar never moved when the dog
        // ate or the bowl was filled.
        FoodBowl bowl = localPackage.GetComponentInChildren<FoodBowl>(true);
        Slider bowlSlider = Slider(authored, "Slider_Bowl");
        if (bowl != null && bowlSlider != null && Set(bowl, "slider", bowlSlider))
        {
            bowlSlider.maxValue = Read(bowl, "maxFood", bowlSlider.maxValue);
            bowlSlider.value = Read(bowl, "currentFood", bowlSlider.value);
            count++;
        }

        DogMoodUI mood = authored.GetComponentInChildren<DogMoodUI>(true);
        if (mood != null && Set(mood, "stateMachine", dog))
        {
            Watch(mood.gameObject, mood, dog);
            count++;
        }

        // The MP prefab's own dog panel (bars + icons) would sit next to the authored one.
        if (count > 0) HideOwn(localPackage, "DogUI", authored);
        return count;
    }

    // The authored HUD outlives the local player (session end, respawn of the package): when what it was pointed at
    // is destroyed, switch the authored component off instead of letting it throw every frame.
    private static void Watch(GameObject host, Behaviour behaviour, UnityEngine.Object target)
    {
        behaviour.enabled = true;

        TargetGuard guard = host.GetComponent<TargetGuard>();
        if (guard == null) guard = host.AddComponent<TargetGuard>();
        guard.Add(behaviour, target);
    }

    [DefaultExecutionOrder(-100)]
    private class TargetGuard : MonoBehaviour
    {
        private readonly System.Collections.Generic.List<(Behaviour behaviour, UnityEngine.Object target)> watched =
            new System.Collections.Generic.List<(Behaviour, UnityEngine.Object)>();

        public void Add(Behaviour behaviour, UnityEngine.Object target)
        {
            watched.RemoveAll(w => w.behaviour == behaviour);
            watched.Add((behaviour, target));
        }

        private void Update()
        {
            foreach ((Behaviour behaviour, UnityEngine.Object target) in watched)
                if (target == null && behaviour != null && behaviour.enabled) behaviour.enabled = false;
        }
    }

    private static int Rebind(MonoBehaviour controller, Slider slider, string maxField, string currentField)
    {
        if (controller == null || slider == null) return 0;
        if (!Set(controller, "slider", slider)) return 0;

        slider.maxValue = Read(controller, maxField, slider.maxValue);
        slider.value = Read(controller, currentField, slider.value);
        return 1;
    }

    private static Slider Slider(Transform root, string name) =>
        root.GetComponentsInChildren<Slider>(true).FirstOrDefault(s => s.name == name);

    // Hides the MP prefab's copy (anything of that name in the local package that is not under the authored root).
    private static void HideOwn(Transform localPackage, string objectName, Transform authored)
    {
        foreach (Transform t in localPackage.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != objectName || t.IsChildOf(authored)) continue;
            if (t.gameObject.activeSelf) t.gameObject.SetActive(false);
        }
    }

    private static bool Set(object target, string field, object value)
    {
        FieldInfo info = target.GetType().GetField(field, AnyField);
        if (info == null) return false;

        info.SetValue(target, value);
        return true;
    }

    private static float Read(object target, string field, float fallback)
    {
        object raw = target.GetType().GetField(field, AnyField)?.GetValue(target);
        return raw == null ? fallback : Convert.ToSingle(raw);
    }
}
