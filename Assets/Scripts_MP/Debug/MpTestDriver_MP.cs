// Multiplayer gameplay test driver. INERT unless the game is started with -mpsmoke.
// Reads commands appended to the file given by -mpsmoke_cmd=<path> (one per line) and runs them through the
// real player interaction code, logging results as "[MPTEST] ..." in Player.log. Used for 2-machine tests.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ItemScript;
using Mirror;
using PlayerScripts;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MpTestDriver_MP : MonoBehaviour
{
    private static int errors, exceptions;
    private static readonly Dictionary<string, int> errorKinds = new Dictionary<string, int>();
    private long bytesIn, bytesOut, msgsIn, msgsOut;
    private string cmdPath;
    private int linesDone;

    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-mpsmoke") < 0) return;
        if (FindAnyObjectByType<MpTestDriver_MP>() != null) return;

        Application.logMessageReceived += (msg, stack, type) =>
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            if (type == LogType.Exception) exceptions++; else errors++;
            string key = msg.Length > 140 ? msg.Substring(0, 140) : msg;
            errorKinds[key] = errorKinds.TryGetValue(key, out int n) ? n + 1 : 1;
        };

        var go = new GameObject("MpTestDriver_MP");
        DontDestroyOnLoad(go);
        go.AddComponent<MpTestDriver_MP>();
    }

    private static void Log(string s) => Debug.Log("[MPTEST] " + s);

    private IEnumerator Start()
    {
        cmdPath = Environment.GetCommandLineArgs().Where(a => a.StartsWith("-mpsmoke_cmd=")).Select(a => a.Substring(13)).FirstOrDefault();
        Log($"driver active, cmd file='{cmdPath}'");

        NetworkDiagnostics.InMessageEvent += m => { msgsIn += m.count; bytesIn += m.bytes * m.count; };
        NetworkDiagnostics.OutMessageEvent += m => { msgsOut += m.count; bytesOut += m.bytes * m.count; };

        while (true)
        {
            string[] lines = Array.Empty<string>();
            try { if (cmdPath != null && File.Exists(cmdPath)) lines = File.ReadAllLines(cmdPath); }
            catch (Exception e) { Log("cmd read failed: " + e.Message); }

            while (linesDone < lines.Length)
            {
                string line = lines[linesDone++].Trim();
                if (line.Length == 0) continue;
                string[] c = line.Split(' ');
                Log("cmd> " + line);
                IEnumerator run = null;
                try { run = Run(c); }
                catch (Exception e) { Log("cmd failed: " + e); }
                if (run != null) yield return SafeRun(run);
                if (c[0] == "quit") yield break;
            }

            yield return new WaitForSeconds(0.25f);
        }
    }

    // Runs a command coroutine, logging instead of dying on exceptions.
    private IEnumerator SafeRun(IEnumerator routine)
    {
        while (true)
        {
            object current;
            try
            {
                if (!routine.MoveNext()) yield break;
                current = routine.Current;
            }
            catch (Exception e)
            {
                Log("cmd exception: " + e.GetType().Name + ": " + e.Message);
                yield break;
            }
            yield return current;
        }
    }

    private static float F(string[] c, int i, float d) =>
        c.Length > i && float.TryParse(c[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : d;

    // ------------------------------------------------------------------ commands

    private IEnumerator Run(string[] c)
    {
        switch (c[0])
        {
            case "ready": return Ready();
            case "start": return StartMatch();
            case "color": return Once(() => Lobby()?.CmdSetColor((int)F(c, 1, 2)));
            case "chat": return Once(() => ChatNetwork.Instance?.Send(string.Join(" ", c.Skip(1))));
            case "rtt": return Rtt(F(c, 1, 5f));
            case "report": return Once(Report);
            case "spawner": return InteractNearest<Spawner_MP>("spawner");
            case "cutload": return InteractNearest<Cutter_MP>("cutter");
            case "cut": return Repeat(() => InteractNearest<Cutter_MP>("cutter"), (int)F(c, 1, 6), 0.35f);
            case "takeproduct": return TakeNearestCarriable(true);
            case "pick": return TakeNearestCarriable(false);
            case "drop": return Drop();
            case "build": return Build();
            case "hammer": return Hammer((int)F(c, 1, 10));
            case "flood": return Flood(F(c, 1, 10f));
            case "car": return Car(F(c, 1, 20f));
            case "emote": return Once(() => Sm()?.ChangeToEmoteState((int)F(c, 1, 1)));
            case "wait": return Wait(F(c, 1, 1f));
            case "quit": return Once(() => { Report(); Application.Quit(0); });
        }
        Log("unknown command " + c[0]);
        return null;
    }

    private static IEnumerator Once(Action a) { a(); yield break; }
    private static IEnumerator Wait(float s) { yield return new WaitForSeconds(s); }

    private static IEnumerator Repeat(Func<IEnumerator> make, int times, float gap)
    {
        for (int i = 0; i < times; i++) { yield return make(); yield return new WaitForSeconds(gap); }
    }

    private IEnumerator Ready()
    {
        for (float t = 0; t < 30f; t += 0.5f)
        {
            LobbyPlayer lp = Lobby();
            if (lp != null) { lp.CmdSetReady(true); Log("ready sent"); yield break; }
            yield return new WaitForSeconds(0.5f);
        }
        Log("ready: no local LobbyPlayer");
    }

    private IEnumerator StartMatch()
    {
        var nm = NetworkManager.singleton as LobbyNetworkManager;
        for (float t = 0; t < 60f; t += 0.5f)
        {
            int players = FindObjectsByType<LobbyPlayer>(FindObjectsSortMode.None).Length;
            if (nm != null && NetworkServer.active && players >= 2 && nm.AllClientsReady())
            {
                nm.StartGame();
                Log($"StartGame with {players} players -> {LobbyNetworkManager.SelectedGameScenePath}");
                yield break;
            }
            yield return new WaitForSeconds(0.5f);
        }
        Log("start: not everyone ready / not the host");
    }

    private IEnumerator Rtt(float seconds)
    {
        var s = new List<double>();
        long bi = bytesIn, bo = bytesOut, mi = msgsIn, mo = msgsOut;
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < seconds) { s.Add(NetworkTime.rtt * 1000.0); yield return new WaitForSeconds(0.1f); }
        float dt = Time.realtimeSinceStartup - t0;
        double avg = s.Average();
        Log($"rtt min {s.Min():0} avg {avg:0} max {s.Max():0} ms jitter {Math.Sqrt(s.Select(x => (x - avg) * (x - avg)).Average()):0} | " +
            $"in {(msgsIn - mi) / dt:0} msg/s {(bytesIn - bi) / dt / 1024f:0.0} KB/s, out {(msgsOut - mo) / dt:0} msg/s {(bytesOut - bo) / dt / 1024f:0.0} KB/s | fps {1f / Time.smoothDeltaTime:0}");
    }

    // Walks (teleports) next to the target and fires the real interact path of PlayerInteractionNetwork_MP.
    private IEnumerator InteractNearest<T>(string label) where T : Component
    {
        Transform me = Body();
        T target = FindObjectsByType<T>(FindObjectsSortMode.None).OrderBy(x => Dist(me, x.transform)).FirstOrDefault();
        if (me == null || target == null) { Log($"{label}: not found"); yield break; }

        // A Cutter_MP's interactable is the Cutter itself; spawners/constructors use their own colliders.
        Collider col = target.GetComponentsInChildren<Collider>().FirstOrDefault(x => x.GetComponent<Interactions.IInteractable>() != null)
                       ?? target.GetComponentInChildren<Collider>();
        yield return Approach(col != null ? col.bounds.center : target.transform.position);
        yield return Interact(label + " '" + target.name + "'");
    }

    private IEnumerator TakeNearestCarriable(bool processedOnly)
    {
        Transform me = Body();
        CarriableObject_MP item = FindObjectsByType<CarriableObject_MP>(FindObjectsSortMode.None)
            .Where(x => x.transform.parent == null && x.Carrier == null &&
                        (!processedOnly || !x.GetComponent<CarriableObject_SP>().isRawMaterial))
            .OrderBy(x => Dist(me, x.transform)).FirstOrDefault();
        if (item == null) { Log("take: no free item"); yield break; }

        yield return ApproachUntilDetected(item.gameObject);
        yield return Interact("take '" + item.name + "' #" + item.netId);
        yield return new WaitForSeconds(0.6f);
        Log($"take result: holding={HeldName()} itemParent={(item != null && item.transform.parent ? item.transform.parent.name : "-")} carrier={(item != null && item.Carrier ? item.Carrier.netId.ToString() : "-")}");
    }

    private IEnumerator Drop()
    {
        PlayerInteractionHandler h = Handler();
        CarriableObject_SP held = h != null ? h.GetCurrentCarriable() : null;
        if (held != null && held.TryGetComponent(out CarriableObject_MP mp)) mp.NotifyDropped(h);
        yield return new WaitForSeconds(0.6f);
        Log("drop -> holding=" + HeldName());
    }

    private IEnumerator Build()
    {
        Transform me = Body();
        CarriableObject_SP held = Handler()?.GetCurrentCarriable();
        if (held == null) { Log("build: not holding anything"); yield break; }

        Constructor target = FindObjectsByType<Constructor>(FindObjectsSortMode.None)
            .Where(k => !k.IsBuilt() && k.GetAcceptedType() == held.carriableType)
            .OrderBy(k => Dist(me, k.transform)).FirstOrDefault();
        if (target == null) { Log($"build: no free constructor for {held.carriableType}"); yield break; }

        yield return ApproachUntilDetected(target.gameObject);
        yield return Interact("build '" + target.name + "'");
        yield return new WaitForSeconds(0.8f);
        Log($"build result: built={target.IsBuilt()} holding={HeldName()}");
        lastBuilt = target;
    }

    private Constructor lastBuilt;

    private IEnumerator Hammer(int hits)
    {
        if (lastBuilt == null) { Log("hammer: build first"); yield break; }
        Component tile = lastBuilt.GetComponentsInChildren<Component>(false).FirstOrDefault(x => x is WoodTile || x is BrickTile || x is GlassTile);
        if (tile == null) { Log("hammer: no active tile"); yield break; }

        yield return ApproachUntilDetected(tile.gameObject);
        for (int i = 0; i < hits && !TileDone(tile); i++)
        {
            yield return Interact(null);
            yield return new WaitForSeconds(0.35f);
        }
        Log($"hammer result: tile '{tile.name}' complete={TileDone(tile)}");
    }

    private IEnumerator Flood(float seconds)
    {
        PlayerInteractionHandler h = Handler();
        long bi = bytesIn, bo = bytesOut; int sent = 0; var r = new List<double>();
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < seconds)
        {
            CarriableObject_SP held = h != null ? h.GetCurrentCarriable() : null;
            if (held != null && held.TryGetComponent(out CarriableObject_MP heldMp)) { heldMp.NotifyDropped(h); sent++; }
            else
            {
                CarriableObject_MP item = FindObjectsByType<CarriableObject_MP>(FindObjectsSortMode.None)
                    .Where(x => x.transform.parent == null && x.Carrier == null && Dist(Body(), x.transform) < 6f)
                    .OrderBy(x => Dist(Body(), x.transform)).FirstOrDefault();
                if (item != null) { item.NotifyPickedUp(h); sent++; }
            }
            r.Add(NetworkTime.rtt * 1000.0);
            yield return new WaitForSeconds(0.025f);
        }
        float dt = Time.realtimeSinceStartup - t0;
        yield return new WaitForSeconds(1f);
        Log($"flood {sent} cmds/{dt:0}s ({sent / dt:0}/s) rtt avg {(r.Count > 0 ? r.Average() : 0):0} max {(r.Count > 0 ? r.Max() : 0):0} ms | " +
            $"in {(bytesIn - bi) / dt / 1024f:0.0} KB/s out {(bytesOut - bo) / dt / 1024f:0.0} KB/s | after: holding={HeldName()}");
    }

    // Stand in front of the nearest moving car and report what happened (stun / death / respawn).
    private IEnumerator Car(float timeout)
    {
        float t0 = Time.realtimeSinceStartup;
        MpCarHitSync_MP car = null;
        while (car == null && Time.realtimeSinceStartup - t0 < timeout)
        {
            car = FindObjectsByType<MpCarHitSync_MP>(FindObjectsSortMode.None).FirstOrDefault();
            if (car == null) yield return new WaitForSeconds(0.5f);
        }
        if (car == null) { Log("car: no car spawned"); yield break; }

        PlayerStateMachine sm = Sm();
        Vector3 ahead = car.transform.position + car.transform.forward * 6f;
        Teleport(ahead, car.transform.position);
        var seen = new HashSet<string>();
        float c0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - c0 < 9f)
        {
            if (sm != null) seen.Add(sm.currentPlayerState.ToString());
            yield return new WaitForSeconds(0.1f);
        }
        var net = Body()?.GetComponent<PlayerNetwork_MP>();
        Log($"car result: states seen=[{string.Join(",", seen)}] isDead={(net != null && net.isDead)} pos={Body()?.position}");
    }

    // ------------------------------------------------------------------ report

    private void Report()
    {
        string scene = SceneManager.GetActiveScene().name;
        Log($"--- report ({(NetworkServer.active ? "HOST" : "CLIENT")}) scene={scene} connected={NetworkClient.isConnected} " +
            $"rtt={NetworkTime.rtt * 1000:0}ms spawned={NetworkClient.spawned.Count} errors={errors} exceptions={exceptions}");

        foreach (PlayerNetwork_MP p in FindObjectsByType<PlayerNetwork_MP>(FindObjectsSortMode.None))
        {
            Animator a = p.GetComponentInChildren<Animator>();
            PlayerStateMachine sm = p.GetComponent<PlayerStateMachine>();
            PlayerInteractionHandler h = p.GetComponent<PlayerInteractionHandler>();
            Log($"  player #{p.netId} {(p.isOwned ? "ME" : "remote")} state={sm?.currentPlayerState} anim={AnimName(a)} " +
                $"carrying={(h != null && h.IsCarrying())} item={(h?.GetCurrentCarriable() ? h.GetCurrentCarriable().name : "-")} dead={p.isDead} pos={p.transform.position}");
        }

        // Dog bars: which controller runs, its value, and which HUD slider it writes to (and what that shows).
        foreach (DogStateMachine dog in FindObjectsByType<DogStateMachine>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            NetworkIdentity owner = dog.GetComponentInParent<NetworkIdentity>(true);
            string who = owner == null ? "scene(SP)" : owner.isOwned ? "MY dog" : "remote dog #" + owner.netId;
            var parts = new List<string>();
            foreach (MonoBehaviour bar in new MonoBehaviour[] { dog.GetComponent<SatietyController>(), dog.GetComponent<FavorController>(), dog.GetComponent<DogEnergyController>() })
            {
                if (bar == null) continue;
                var slider = bar.GetType().GetField("slider", Any)?.GetValue(bar) as UnityEngine.UI.Slider;
                string sliderInfo = slider == null ? "no slider" :
                    $"slider={slider.value:0}/{slider.maxValue:0} active={slider.gameObject.activeInHierarchy} in='{slider.transform.root.name}'" +
                    $"{(slider.GetComponentInParent<NetworkIdentity>(true) == owner ? "" : " (OTHER package!)")}";
                parts.Add($"{bar.GetType().Name}[{(bar.enabled ? "on" : "OFF")}/{(bar.gameObject.activeInHierarchy ? "act" : "INACTIVE")}] {sliderInfo}");
            }
            Log($"  dog {who} '{dog.name}' sm={(dog.enabled ? "on" : "OFF")} active={dog.gameObject.activeInHierarchy} | {string.Join(" | ", parts)}");
        }

        foreach (CarriableObject_MP c in FindObjectsByType<CarriableObject_MP>(FindObjectsSortMode.None).OrderBy(x => x.netId).Take(10))
            Log($"  item #{c.netId} {c.name} carrier={(c.Carrier ? c.Carrier.netId.ToString() : "-")} parent={(c.transform.parent ? c.transform.parent.name : "-")} visible={c.GetComponentsInChildren<Renderer>().Any(r => r.enabled && r.gameObject.activeInHierarchy)}");

        int built = FindObjectsByType<Constructor>(FindObjectsSortMode.None).Count(k => k.IsBuilt());
        int tilesDone = FindObjectsByType<WoodTile>(FindObjectsSortMode.None).Count(t => t.GetCurrentPhase() == ConstructionPhase.Complete)
                      + FindObjectsByType<BrickTile>(FindObjectsSortMode.None).Count(t => t.GetCurrentPhase() == ConstructionPhase.Complete)
                      + FindObjectsByType<GlassTile>(FindObjectsSortMode.None).Count(t => t.GetCurrentPhase() == ConstructionPhase.Complete);
        GameTimeManager time = GameTimeManager.Instance;
        Log($"  world: constructors built={built} tiles complete={tilesDone} timer={(time != null ? time.GetCurrentTime().ToString("0.0") : "-")} " +
            $"active={(time != null && time.IsTimerActive())} generators=[{string.Join(",", FindObjectsByType<Generator_Prototype>(FindObjectsSortMode.None).Select(g => g.GetEnergy().ToString("0")))}]");

        foreach (var kv in errorKinds.OrderByDescending(k => k.Value).Take(10)) Log($"  err x{kv.Value}: {kv.Key}");
    }

    private static readonly string[] AnimNames =
        { "Empty", "Idle", "Walk", "Run", "CarryIdle", "CarryWalk", "CarryRun", "Jump", "Interact", "Stand", "Slip", "Dance", "Search", "Throw", "UseED", "UseFE" };

    private static string AnimName(Animator a)
    {
        if (a == null) return "none";
        if (!a.enabled) return "OFF(ragdoll)";
        int h = a.GetCurrentAnimatorStateInfo(0).shortNameHash;
        foreach (string n in AnimNames) if (Animator.StringToHash(n) == h) return n;
        return h.ToString();
    }

    // ------------------------------------------------------------------ helpers

    private static LobbyPlayer Lobby() => NetworkClient.localPlayer != null ? NetworkClient.localPlayer.GetComponent<LobbyPlayer>() : null;
    private static PlayerNetwork_MP Me() => NetworkClient.localPlayer != null ? NetworkClient.localPlayer.GetComponentInChildren<PlayerNetwork_MP>(true) : null;
    private static Transform Body() => Me() != null ? Me().transform : null;
    private static PlayerInteractionHandler Handler() => Me() != null ? Me().GetComponent<PlayerInteractionHandler>() : null;
    private static PlayerStateMachine Sm() => Me() != null ? Me().GetComponent<PlayerStateMachine>() : null;
    private static string HeldName() { var h = Handler(); return h != null && h.GetCurrentCarriable() != null ? h.GetCurrentCarriable().name : "nothing"; }
    private static float Dist(Transform a, Transform b) => a == null || b == null ? float.MaxValue : Vector3.Distance(a.position, b.position);

    private static bool TileDone(Component t) =>
        t is WoodTile w ? w.GetCurrentPhase() == ConstructionPhase.Complete :
        t is BrickTile b ? b.GetCurrentPhase() == ConstructionPhase.Complete :
        t is GlassTile g && g.GetCurrentPhase() == ConstructionPhase.Complete;

    private static void Teleport(Vector3 position, Vector3 lookAt)
    {
        Transform me = Body();
        if (me == null) return;
        Vector3 flat = lookAt - position; flat.y = 0f;
        Quaternion rot = flat.sqrMagnitude > 0.001f ? Quaternion.LookRotation(flat) : me.rotation;
        if (me.TryGetComponent(out Rigidbody rb)) { rb.position = position; rb.rotation = rot; rb.linearVelocity = Vector3.zero; }
        me.SetPositionAndRotation(position, rot);
    }

    // Stand ~1 m from the target on the near side, facing it.
    private static IEnumerator Approach(Vector3 target)
    {
        Transform me = Body();
        if (me == null) yield break;
        Vector3 dir = me.position - target; dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
        Vector3 stand = target + dir.normalized * 1.0f;
        stand.y = me.position.y;
        Teleport(stand, target);
        yield return new WaitForSeconds(0.3f);
    }

    // Like a player walking around an object until the interact prompt shows it: try 8 spots around it.
    private static IEnumerator ApproachUntilDetected(GameObject target)
    {
        PlayerInteractionNetwork_MP net = Me() != null ? Me().GetComponent<PlayerInteractionNetwork_MP>() : null;
        Collider col = target.GetComponentInChildren<Collider>();
        Vector3 center = col != null ? col.bounds.center : target.transform.position;
        Transform me = Body();
        if (net == null || me == null) yield break;

        for (int i = 0; i < 16; i++)
        {
            float radius = i < 8 ? 1.0f : 1.4f;
            Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
            Vector3 stand = center + dir * radius;
            stand.y = me.position.y;
            Teleport(stand, center);
            yield return new WaitForSeconds(0.15f);

            RefreshTargetM?.Invoke(net, null);
            if (CurrentInteractableF?.GetValue(net) is Component seen && seen != null &&
                (seen.gameObject == target || seen.transform.IsChildOf(target.transform) || target.transform.IsChildOf(seen.transform)))
                yield break;
        }
        Log($"approach: never detected '{target.name}' from 16 spots");
    }

    private static readonly MethodInfo RefreshTargetM = typeof(PlayerInteractionNetwork_MP).GetMethod("RefreshTarget", Any);
    private static readonly MethodInfo HandleInteractM = typeof(PlayerInteractionNetwork_MP).GetMethod("HandleInteract", Any);
    private static readonly FieldInfo CurrentInteractableF = typeof(PlayerInteractionNetwork_MP).GetField("currentInteractable", Any);

    // Same as pressing the interact key: detection cast, then HandleInteract.
    private static IEnumerator Interact(string label)
    {
        PlayerInteractionNetwork_MP net = Me() != null ? Me().GetComponent<PlayerInteractionNetwork_MP>() : null;
        if (net == null) { Log("interact: no local player"); yield break; }

        RefreshTargetM?.Invoke(net, null);
        object target = CurrentInteractableF?.GetValue(net);
        if (label != null) Log($"interact {label}: detected '{(target as Component)?.name ?? "nothing"}' holding={HeldName()}");
        HandleInteractM?.Invoke(net, null);
        yield return new WaitForSeconds(0.2f);
    }
}
