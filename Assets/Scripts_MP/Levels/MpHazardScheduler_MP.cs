using System.Collections;
using System.Reflection;
using Mirror;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// Takes over the level's HazardController_Prototype (the ones whose events trigger telegraphed hazards).
// SP's controller rolls its own random hazard on every machine; here it is switched off everywhere and the SERVER
// runs the same schedule (initial delay, random interval, random event) and starts the chosen hazards on all
// machines through Hazard_MP. Controllers that do something else (e.g. the "aid" controller) are left alone.
public class MpHazardScheduler_MP : MonoBehaviour
{
    private const BindingFlags NonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly FieldInfo EventsField = typeof(HazardController_Prototype).GetField("availableHazards", NonPublic);
    private static readonly FieldInfo MinField = typeof(HazardController_Prototype).GetField("minHazardInterval", NonPublic);
    private static readonly FieldInfo MaxField = typeof(HazardController_Prototype).GetField("maxHazardInterval", NonPublic);
    private static readonly FieldInfo DelayField = typeof(HazardController_Prototype).GetField("initialDelay", NonPublic);

    // Called on every machine when a level scene is installed (before the controllers' Start).
    public static void BindScene(Scene scene)
    {
        if (!scene.IsValid() || EventsField == null) return;
        if (!NetworkServer.active && !NetworkClient.active) return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (HazardController_Prototype controller in root.GetComponentsInChildren<HazardController_Prototype>(true))
            {
                if (!controller.enabled) continue; // already taken over (or off in the scene)

                UnityEvent[] events = EventsField.GetValue(controller) as UnityEvent[];
                if (!TargetsHazards(events)) continue;

                controller.CancelInvoke();
                controller.StopAllCoroutines();
                controller.enabled = false;

                if (!NetworkServer.active) continue;

                var host = new GameObject("MpHazardScheduler_MP (" + controller.name + ")");
                host.AddComponent<MpHazardScheduler_MP>().StartSchedule(
                    events,
                    (float)MinField.GetValue(controller),
                    (float)MaxField.GetValue(controller),
                    (float)DelayField.GetValue(controller));

                InteractionDebug.Log($"[MP-HAZARD] '{controller.name}' taken over by the server " +
                                     $"(events={events.Length}, delay={DelayField.GetValue(controller)}).");
            }
        }
    }

    private static bool TargetsHazards(UnityEvent[] events)
    {
        if (events == null) return false;

        foreach (UnityEvent hazardEvent in events)
        {
            if (hazardEvent == null) continue;

            for (int i = 0; i < hazardEvent.GetPersistentEventCount(); i++)
                if (hazardEvent.GetPersistentTarget(i) is TelegraphedHazard) return true;
        }

        return false;
    }

    private void StartSchedule(UnityEvent[] events, float min, float max, float delay) =>
        StartCoroutine(Schedule(events, min, max, delay));

    private static IEnumerator Schedule(UnityEvent[] events, float min, float max, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (events == null || events.Length == 0) yield break;

        while (true)
        {
            TriggerEvent(events[Random.Range(0, events.Length)]);
            yield return new WaitForSeconds(Random.Range(min, max));
        }
    }

    private static void TriggerEvent(UnityEvent hazardEvent)
    {
        if (hazardEvent == null || !NetworkServer.active) return;

        for (int i = 0; i < hazardEvent.GetPersistentEventCount(); i++)
        {
            if (!(hazardEvent.GetPersistentTarget(i) is TelegraphedHazard hazard)) continue;
            if (hazardEvent.GetPersistentMethodName(i) != "OnTrigger") continue;

            Hazard_MP networked = hazard.GetComponent<Hazard_MP>();
            if (networked != null) networked.ServerTelegraph();
        }
    }
}
