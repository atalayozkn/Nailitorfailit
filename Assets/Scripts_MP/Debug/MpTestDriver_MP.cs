// Multiplayer gameplay test driver. INERT unless the game is started with -mpsmoke.
// Reads commands appended to the file given by -mpsmoke_cmd=<path> (one per line) and runs them through the
// real player interaction code, logging results as "[MPTEST] ..." in Player.log. Used for 2-machine tests.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Interactions.Networking;
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
            case "mpmenu": return Once(() => MpMenuProbe(c.Length > 1 ? c[1] : "state"));
            case "char": return Once(() => Lobby()?.CmdSetCharacter((int)F(c, 1, 1)));
            case "color": return Once(() => Lobby()?.CmdSetColor((int)F(c, 1, 2)));
            case "chat": return Once(() => ChatNetwork.Instance?.Send(string.Join(" ", c.Skip(1))));
            case "rtt": return Rtt(F(c, 1, 5f));
            case "report": return Once(Report);
            case "slot": return Once(() => Me()?.GetComponent<PlayerInteractionNetwork_MP>()?.RequestSlot((int)F(c, 1, 0)));
            case "mic": return Once(() => Me()?.CmdSetMicMuted(F(c, 1, 1) > 0.5f));
            case "syncwait": return SyncWait(F(c, 1, 6f));
            case "racetake": return RaceTake();
            case "racerun": return RaceRun((int)F(c, 1, 8));
            case "takeapi": return TakeApi(c.Length > 1 ? c[1] : null);
            case "buildapi": return BuildApi();
            case "hammerapi": return HammerApi((int)F(c, 1, 12));
            case "chatopen": return Once(ChatOpenProbe);
            case "dogs": return Once(LogDogs);
            case "bars": return Once(LogBars);
            case "level": return Once(() => { MpLevelCatalog.Instance.Select((int)F(c, 1, 0f)); Log($"level selected: {MpLevelCatalog.Instance.SelectedIndex}"); });
            case "mixtest": return MixTest(c.Length > 1 ? c[1] : "glass");
            case "endtest": return Once(EndTest);
            case "fetest": return FeTest();
            case "mailman": return Once(LogMailMan);
            case "solostart": return Once(() => { (NetworkManager.singleton as LobbyNetworkManager)?.StartGame(); Log("solostart requested"); });
            case "lamppairs": return Once(LogLampPairs);
            case "lampnear": return LampNear();
            case "throwtest": return ThrowTest(c.Length > 1 ? c[1] : "Carriable_Wood_Processed_MP");
            case "flingall": return Once(() =>
            {
                float speed = F(c, 1, 9f);
                foreach (PlayerNetwork_MP p in FindObjectsByType<PlayerNetwork_MP>(FindObjectsSortMode.None))
                    p.ServerStunFling(p.transform.forward * -speed + Vector3.up * 5f);
                Log($"flingall speed={speed}");
            });
            case "lamps": return Once(LogLamps);
            case "satiety": return Once(() => SetSatiety(F(c, 1, 61f)));
            case "posewatch": return Once(() => StartCoroutine(PoseWatch()));
            case "objs": return Once(() => LogObjects(c.Length > 1 ? c[1] : "lamp"));
            case "inv": return Once(() => StartCoroutine(InvariantWatch(F(c, 1, 60f))));
            case "spawner": return InteractNearest<Spawner_MP>("spawner");
            case "cutload": return InteractNearest<Cutter_MP>("cutter");
            case "cut": return Repeat(() => InteractNearest<Cutter_MP>("cutter"), (int)F(c, 1, 6), 0.35f);
            case "takeproduct": return TakeNearestCarriable(true, c.Length > 1 ? c[1] : null);
            case "pick": return TakeNearestCarriable(false, c.Length > 1 ? c[1] : null);
            case "drop": return Drop();
            case "build": return Build();
            case "hammer": return Hammer((int)F(c, 1, 10));
            case "flood": return Flood(F(c, 1, 10f));
            case "car": return Car(F(c, 1, 20f));
            case "carwatch": return CarWatch(F(c, 1, 10f));
            case "cars": return CarsLog(F(c, 1, 30f));
            case "use": return UseItem(F(c, 1, 5f));
            case "killremote": return Once(() =>
            {
                PlayerNetwork_MP remote = FindObjectsByType<PlayerNetwork_MP>(FindObjectsSortMode.None).FirstOrDefault(p => !p.isOwned);
                if (remote == null || !NetworkServer.active) { Log("killremote: no remote player"); return; }
                Log($"killremote: killing remote player at {remote.transform.position}");
                remote.ServerKill(DeathReason.None);
            });
            case "fall": return Once(() =>
            {
                PlayerNetwork_MP p = Me();
                if (p == null) { Log("fall: no local player"); return; }
                var rb = p.GetComponent<Rigidbody>();
                Vector3 t = p.transform.position + Vector3.down * 40f;
                p.transform.position = t;
                if (rb != null) rb.position = t;
                Log($"fall: moved local player to y={t.y:0}");
            });
            case "chatenter": return Once(() =>
            {
                // The chat input as the player uses it: text in the field, Enter (onSubmit), no Send button needed.
                string text = string.Join(" ", c.Skip(1));
                foreach (TMPro.TMP_InputField field in FindObjectsByType<TMPro.TMP_InputField>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!field.gameObject.activeInHierarchy || field.GetComponentInParent<ChatUI>() == null) continue;
                    field.text = text;
                    field.onSubmit.Invoke(text);
                    Log($"chatenter: submitted '{text}' via {field.GetComponentInParent<ChatUI>().name}");
                    return;
                }
                Log("chatenter: no active chat input");
            });
            case "dropchase": return DropChase((int)F(c, 1, 12f));
            case "feedwood": return FeedWood((int)F(c, 1, 10f));
            case "realpickc": return RealPickClient((int)F(c, 1, 10f));
            case "realpick": return RealPick((int)F(c, 1, 10f), c.Length > 2 ? c[2] : "Carriable_Wood_Processed_MP");
            case "mixerwatch": return MixerWatch(F(c, 1, 60f));
            case "mixerui": return Once(() =>
            {
                foreach (MixerUIHelper u in FindObjectsByType<MixerUIHelper>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    string letters = string.Concat(u.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Select(t => "[" + t.text + "]"));
                    Log($"mixerui {u.transform.root.name}/{u.name}: {letters}");
                }
            });
            case "stun": return Once(() => Me()?.RequestStun());
            case "hazards": return Once(LogHazards);
            case "homes": return Once(LogHomes);
            case "repair": return Once(RepairHazard);
            case "ragwatch": return RagWatch(F(c, 1, 6f));
            case "emote": return Once(() => Sm()?.ChangeToEmoteState((int)F(c, 1, 1)));
            case "wait": return Wait(F(c, 1, 1f));
            case "quit": return Once(() => { Report(); Application.Quit(0); });
        }
        Log("unknown command " + c[0]);
        return null;
    }

    // Background: logs every change of (state, renderer, bent pose) of every player as this machine sees it.
    private IEnumerator PoseWatch()
    {
        var last = new Dictionary<uint, string>();
        float t0 = Time.realtimeSinceStartup;
        while (true)
        {
            foreach (PlayerNetwork_MP p in FindObjectsByType<PlayerNetwork_MP>(FindObjectsSortMode.None))
            {
                PlayerStateMachine sm = p.GetComponent<PlayerStateMachine>();
                Animator anim = sm != null ? sm.animator : null;
                SkinnedMeshRenderer smr = anim != null ? anim.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
                bool on = smr != null && smr.enabled && smr.gameObject.activeInHierarchy;
                float h = smr != null ? smr.bounds.size.y : -1f;
                float tilt = anim != null ? anim.transform.up.y : 1f;
                bool bent = on && (h < 1.25f || tilt < 0.85f);
                string name = anim != null && anim.enabled && anim.isActiveAndEnabled ? AnimName(anim) : "animOff";
                string ragMode = "-";
                var ar = p.GetComponent<PlayerActiveRagdoll>();
                if (ar != null && ar.enabled) ragMode = typeof(PlayerActiveRagdoll).GetField("mode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(ar)?.ToString() ?? "?";
                Rigidbody hipsRb = p.RagdollHips;
                float hipsHigh = hipsRb != null ? hipsRb.position.y - p.transform.position.y : -9f;
                float hipsSide = hipsRb != null ? Vector2.Distance(new Vector2(hipsRb.position.x, hipsRb.position.z), new Vector2(p.transform.position.x, p.transform.position.z)) : -9f;
                string key = $"{sm?.currentPlayerState} dead={p.isDead} smr={(on ? "on" : "off")} bent={bent} anim={name} rag={ragMode} hipsHigh={Mathf.Round(hipsHigh * 3f) / 3f:0.0} hipsSide={Mathf.Round(hipsSide * 3f) / 3f:0.0}";
                if (!last.TryGetValue(p.netId, out string prev) || prev != key)
                {
                    last[p.netId] = key;
                    Log($"pose t={Time.realtimeSinceStartup - t0:0.0} player#{p.netId} {(p.isOwned ? "ME" : "remote")} {key} h={h:0.00} tilt={tilt:0.00} root={p.transform.position:0.0}");
                }
            }
            yield return new WaitForSeconds(0.2f);
        }
    }

    // Test: sets the local dog's hunger value (it goes to the bowl to eat when it reaches 60 / 40 / 20 / 10).
    private void SetSatiety(float value)
    {
        foreach (SatietyController s in FindObjectsByType<SatietyController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!s.isActiveAndEnabled) continue;
            typeof(SatietyController).GetField("currentSatiety", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(s, value);
            Log($"satiety set to {value} on '{s.transform.root.name}'");
        }
    }

    private void LogLamps()
    {
        int total = 0, oldVisible = 0, oldHidden = 0;
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!t.name.StartsWith("StyleTest_LampPost")) continue;
            total++;

            Transform original = t.parent;
            var mr = original != null ? original.GetComponent<MeshRenderer>() : null;
            if (mr != null && mr.enabled && mr.gameObject.activeInHierarchy)
            {
                oldVisible++;
                if (oldVisible <= 3) Log($"lamp: original mesh '{original.name}' at {original.position:0.0} still VISIBLE next to the new post");
            }
            else oldHidden++;
        }

        Log($"lamps: new posts={total} originalVisible={oldVisible} originalHidden={oldHidden}");
    }

    // Mixer through the real item flow: pick each ingredient up, put it in the mixer, crank, see what comes out.
    private IEnumerator MixTest(string recipe)
    {
        PlayerNetwork_MP me = Me();
        Mixer mixer = FindFirstObjectByType<Mixer>();
        if (me == null || mixer == null || !NetworkServer.active) { Log("mixtest: needs the host and a Mixer in the scene"); yield break; }

        PlayerInteractionHandler handler = me.GetComponent<PlayerInteractionHandler>();
        string main = recipe == "brick" ? "Carriable_Brick_MP" : "Carriable_Glass_Raw_MP";
        string[] names = recipe == "fail" ? new[] { "Carriable_Wood_Raw_MP", "Carriable_Wood_Raw_MP", "Carriable_Glass_Raw_MP" } : new[] { main, main, "Carriable_Cement_MP" };
        int badStates = 0;

        foreach (string n in names)
        {
            var prefab = NetworkManager.singleton.spawnPrefabs.FirstOrDefault(g => g != null && g.name == n);
            if (prefab == null) { Log($"mixtest: prefab '{n}' missing"); yield break; }

            GameObject g2 = Instantiate(prefab, me.transform.position + me.transform.forward * 1.0f + Vector3.up, Quaternion.identity);
            NetworkServer.Spawn(g2);
            yield return new WaitForSeconds(0.3f);

            var item = g2.GetComponent<CarriableObject_MP>();
            item.NotifyPickedUp(handler);
            yield return new WaitForSeconds(0.5f);

            bool holding = handler.GetCurrentCarriable() == g2.GetComponent<CarriableObject_SP>();
            bool parented = g2.transform.parent == handler.GetCarryTransform();
            if (!holding || !parented) { badStates++; Log($"mixtest: {n} pickup holding={holding} parented={parented}"); }

            mixer.ServerInteract(me.netIdentity);
            yield return new WaitForSeconds(0.5f);

            bool handEmpty = handler.GetCurrentCarriable() == null;
            if (!handEmpty) { badStates++; Log($"mixtest: {n} hand not empty after the mixer took it"); }
        }

        int before = FindObjectsByType<CarriableObject_SP>(FindObjectsSortMode.None).Count(x => x.carriableType == (recipe == "brick" ? CarriableType.Brick : CarriableType.Glass) && !x.isRawMaterial);
        mixer.ServerCompare(me.netIdentity);
        yield return new WaitForSeconds(1.5f);
        int after = FindObjectsByType<CarriableObject_SP>(FindObjectsSortMode.None).Count(x => x.carriableType == (recipe == "brick" ? CarriableType.Brick : CarriableType.Glass) && !x.isRawMaterial);
        Log($"MIXTEST {recipe}: processed before={before} after={after} badStates={badStates} handEmpty={handler.GetCurrentCarriable() == null}");

        if (recipe == "fail")
        {
            // The rejected ingredients were thrown out: can the player pick one up again?
            yield return new WaitForSeconds(1.5f);
            CarriableObject_MP dropped = FindObjectsByType<CarriableObject_MP>(FindObjectsSortMode.None)
                .Where(x => x.Carrier == null && x.transform.parent == null)
                .OrderBy(x => Vector3.Distance(x.transform.position, mixer.transform.position)).FirstOrDefault();
            if (dropped == null) { Log("MIXTEST fail: nothing was dropped"); yield break; }

            dropped.NotifyPickedUp(handler);
            yield return new WaitForSeconds(0.8f);
            bool got = handler.GetCurrentCarriable() == dropped.GetComponent<CarriableObject_SP>() && dropped.Carrier != null;
            Log($"MIXTEST fail: picked a dropped ingredient again = {got} ('{dropped.name}')");
        }
    }

    // Throw emote with a networked item in hand: does it leave the hand and fly?
    private IEnumerator ThrowTest(string prefabName)
    {
        PlayerNetwork_MP me = Me();
        PlayerStateMachine sm = Sm();
        if (me == null || sm == null || !NetworkServer.active) { Log("throwtest: needs the host"); yield break; }

        var prefab = NetworkManager.singleton.spawnPrefabs.FirstOrDefault(g => g != null && g.name == prefabName);
        if (prefab == null) { Log($"throwtest: prefab '{prefabName}' missing"); yield break; }

        PlayerInteractionHandler handler = me.GetComponent<PlayerInteractionHandler>();
        GameObject g2 = Instantiate(prefab, me.transform.position + me.transform.forward * 1.0f + Vector3.up, Quaternion.identity);
        NetworkServer.Spawn(g2);
        yield return new WaitForSeconds(0.3f);

        var item = g2.GetComponent<CarriableObject_MP>();
        item.NotifyPickedUp(handler);
        yield return new WaitForSeconds(0.6f);

        Vector3 start = g2.transform.position;
        sm.ChangeToEmoteState(0);
        float t0 = Time.realtimeSinceStartup;
        float maxDist = 0f;
        while (Time.realtimeSinceStartup - t0 < 4.5f)
        {
            if (g2 == null) break;
            maxDist = Mathf.Max(maxDist, Vector3.Distance(g2.transform.position, start));
            if (Mathf.Abs((Time.realtimeSinceStartup - t0) % 0.5f) < 0.11f)
            {
                var at = typeof(PlayerNetwork_MP).GetField("emoteThrowAt", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(me);
                Log($"throwtest t={Time.realtimeSinceStartup - t0:0.0} time={Time.time:0.0} scale={Time.timeScale} state={sm.currentPlayerState} emoteIdx={sm.activeEmoteIndex} emoteThrowAt={at} held={(handler.GetCurrentCarriable() != null)} owned={me.isOwned}");
            }
            yield return new WaitForSeconds(0.1f);
        }

        Log($"THROWTEST {prefabName}: maxDist={maxDist:0.0} carrierNow={(item != null && item.Carrier != null ? "yes" : "no")} handEmpty={handler.GetCurrentCarriable() == null} state={sm.currentPlayerState}");
    }

    // Forces the end-of-match panel and lists every active button under any 'GameEnd' panel.
    private void EndTest()
    {
        var hud = FindObjectsByType<GameHUD_MP>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(h => h.isActiveAndEnabled);
        if (hud == null) { Log("endtest: no GameHUD_MP"); return; }

        typeof(GameHUD_MP).GetMethod("OnGameEnded", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(hud, new object[] { true });

        foreach (UnityEngine.UI.Button b in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string path = b.name;
            for (Transform t = b.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
            if (path.IndexOf("GameEnd", StringComparison.OrdinalIgnoreCase) < 0 && b.name.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) < 0) continue;

            var rt = (RectTransform)b.transform;
            Log($"endtest button '{path}' activeInHierarchy={b.gameObject.activeInHierarchy} interactable={b.interactable} size={rt.rect.size.x * rt.lossyScale.x:0}x{rt.rect.size.y * rt.lossyScale.y:0} pos={rt.position:0}");
        }
    }

    private void LogMailMan()
    {
        var bf = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (MailManController m in FindObjectsByType<MailManController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var agent = m.GetComponent<UnityEngine.AI.NavMeshAgent>();
            var path = typeof(MailManController).GetField("path", bf)?.GetValue(m) as Transform[];
            int idx = (int)(typeof(MailManController).GetField("currentPathIndex", bf)?.GetValue(m) ?? -1);
            var renderers = m.GetComponentsInChildren<Renderer>(true);
            int visible = renderers.Count(r => r.enabled && r.gameObject.activeInHierarchy);
            Log($"mailman '{m.transform.root.name}/{m.name}' activeInHierarchy={m.gameObject.activeInHierarchy} enabled={m.enabled} pos={m.transform.position:0.0} " +
                $"agent={(agent != null ? (agent.enabled ? (agent.isOnNavMesh ? "onNavMesh" : "OFF navmesh") : "disabled") : "none")} pathLen={(path != null ? path.Length : -1)} idx={idx} visibleRenderers={visible}/{renderers.Length} " +
                $"owned={(m.GetComponentInParent<PlayerNetwork_MP>(true)?.isOwned)}");
        }

        // Who switches the mail man on? Every UnityEvent in the scene that targets it.
        var mailMen = FindObjectsByType<MailManController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null) continue;
            foreach (FieldInfo f in mb.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var events = new List<UnityEngine.Events.UnityEventBase>();
                object v = null;
                try { v = f.GetValue(mb); } catch { }
                if (v is UnityEngine.Events.UnityEventBase single) events.Add(single);
                else if (v is UnityEngine.Events.UnityEventBase[] many) events.AddRange(many);
                foreach (var ev in events)
                    for (int i = 0; i < ev.GetPersistentEventCount(); i++)
                    {
                        UnityEngine.Object target = ev.GetPersistentTarget(i);
                        GameObject targetGo = target as GameObject ?? (target as Component)?.gameObject;
                        if (targetGo == null) continue;
                        if (mailMen.Any(m => m != null && (m.gameObject == targetGo || targetGo.transform.IsChildOf(m.transform.parent ?? m.transform))))
                            Log($"mailman event: {mb.GetType().Name} '{mb.transform.root.name}/{mb.name}' field {f.Name}[{i}] -> '{targetGo.transform.root.name}/{targetGo.name}'.{ev.GetPersistentMethodName(i)}");
                    }
            }
        }

        foreach (MailBox box in FindObjectsByType<MailBox>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Log($"mailbox '{box.transform.root.name}/{box.name}' active={box.gameObject.activeInHierarchy} pos={box.transform.position:0.0}");
    }

    // Walks the player up to several lamps and counts old lamp meshes that are visible next to the new post.
    private IEnumerator LampNear()
    {
        PlayerNetwork_MP me = Me();
        if (me == null) { Log("lampnear: no local player"); yield break; }

        Transform[] posts = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(t => t.name.StartsWith("StyleTest_LampPost")).Take(10).ToArray();
        int bad = 0, lodGroups = 0;
        foreach (Transform post in posts)
        {
            if (post.GetComponentInParent<LODGroup>() != null) lodGroups++;
            Teleport(post.position + Vector3.right * 1.5f, post.position);
            yield return new WaitForSeconds(0.6f);

            var old = post.parent != null ? post.parent.GetComponent<MeshRenderer>() : null;
            if (old != null && old.enabled && old.gameObject.activeInHierarchy)
            {
                bad++;
                Log($"lampnear: old lamp mesh '{old.name}' at {post.position:0.0} is VISIBLE next to the post");
            }
        }

        Log($"LAMPNEAR: {posts.Length} lamps visited, oldVisible={bad}, underLODGroup={lodGroups}");
    }

    private void LogLampPairs()
    {
        var list = new List<Renderer>();
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            Bounds b = r.bounds;
            if (b.size.y < 2.0f || b.size.y > 7f || b.size.x > 3f || b.size.z > 3f) continue;
            string n = r.name.ToLower(), pn = r.transform.parent ? r.transform.parent.name.ToLower() : "", rn = r.transform.root.name.ToLower();
            if (n.IndexOf("lamp") < 0 && n.IndexOf("lamb") < 0 && n.IndexOf("plane.001") < 0 && pn.IndexOf("lamb") < 0 && pn.IndexOf("lamp") < 0 && n.IndexOf("cylinder") < 0 && n.IndexOf("direk") < 0) continue;
            list.Add(r);
        }

        int pairs = 0;
        for (int i = 0; i < list.Count; i++)
            for (int j = i + 1; j < list.Count; j++)
            {
                Vector3 a = list[i].bounds.center, c = list[j].bounds.center;
                if (Vector2.Distance(new Vector2(a.x, a.z), new Vector2(c.x, c.z)) > 1.6f) continue;
                pairs++;
                if (pairs <= 12)
                    Log($"lamppair '{list[i].name}'<{list[i].transform.parent?.name}> + '{list[j].name}'<{list[j].transform.parent?.name}> at {a:0.0} sizes {list[i].bounds.size:0.0} / {list[j].bounds.size:0.0}");
            }

        Log($"lamppairs: tall lamp-like visible renderers={list.Count} overlapping pairs={pairs}");
    }

    // Networked fire extinguisher: how many uses until it is gone?
    private IEnumerator FeTest()
    {
        PlayerNetwork_MP me = Me();
        if (me == null || !NetworkServer.active) { Log("fetest: needs the host"); yield break; }

        var prefab = NetworkManager.singleton.spawnPrefabs.FirstOrDefault(g => g != null && g.name == "FireExtinguisher_MP");
        if (prefab == null) { Log("fetest: prefab missing"); yield break; }

        PlayerInteractionHandler handler = me.GetComponent<PlayerInteractionHandler>();
        GameObject g2 = Instantiate(prefab, me.transform.position + me.transform.forward + Vector3.up, Quaternion.identity);
        NetworkServer.Spawn(g2);
        yield return new WaitForSeconds(0.3f);
        g2.GetComponent<CarriableObject_MP>().NotifyPickedUp(handler);
        yield return new WaitForSeconds(0.5f);

        var fe = g2.GetComponent<FireExtinguisher>();
        int uses = 0;
        for (int i = 1; i <= 9 && g2 != null; i++)
        {
            fe.OnUse();
            uses = i;
            yield return new WaitForSeconds(0.4f);
        }

        Log($"FETEST: object gone after {uses} OnUse call(s) (gone={g2 == null})");
    }

    private void LogBars()
    {
        foreach (UnityEngine.UI.Slider s in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Slider>())
        {
            if (!s.gameObject.scene.IsValid() || !s.name.StartsWith("Slider_")) continue;
            Log($"bar '{s.transform.parent?.parent?.parent?.name}/{s.name}' value={s.value:0}/{s.maxValue:0} visible={s.gameObject.activeInHierarchy}");
        }
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

    private IEnumerator TakeNearestCarriable(bool processedOnly, string nameFilter = null)
    {
        Transform me = Body();
        CarriableObject_MP item = FindObjectsByType<CarriableObject_MP>(FindObjectsSortMode.None)
            .Where(x => x.transform.parent == null && x.Carrier == null &&
                        (!processedOnly || !x.GetComponent<CarriableObject_SP>().isRawMaterial) &&
                        (string.IsNullOrEmpty(nameFilter) || x.name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0))
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
    // Logs every networked car that is near a player (position, speed, kinematic flags, nearest player's state)
    // four times a second: shows whether a car stops against a player and which machine moves what.
    // Every second: each live car (position, direction, speed) and the closest oncoming car that drives on the same road
    // line, with the sideways gap between them. Used to check the lane separation and the speeds.
    private IEnumerator CarsLog(float seconds)
    {
        float t0 = Time.realtimeSinceStartup;
        float worstGap = 999f;
        var speeds = new List<float>();

        while (Time.realtimeSinceStartup - t0 < seconds)
        {
            MpCarHitSync_MP[] cars = FindObjectsByType<MpCarHitSync_MP>(FindObjectsSortMode.None);

            foreach (MpCarHitSync_MP car in cars)
            {
                Rigidbody rb = car.GetComponent<Rigidbody>();
                if (rb == null) continue;
                Vector3 v = rb.linearVelocity; v.y = 0f;
                if (v.magnitude < 0.5f) continue;
                speeds.Add(v.magnitude);

                Vector3 dir = v.normalized;
                Vector3 right = new Vector3(dir.z, 0f, -dir.x);
                float bestGap = 999f;

                foreach (MpCarHitSync_MP other in cars)
                {
                    if (other == car) continue;
                    Rigidbody orb = other.GetComponent<Rigidbody>();
                    if (orb == null) continue;
                    Vector3 ov = orb.linearVelocity; ov.y = 0f;
                    if (ov.magnitude < 0.5f || Vector3.Dot(ov.normalized, dir) > -0.9f) continue;

                    float lateral = Mathf.Abs(Vector3.Dot(other.transform.position - car.transform.position, right));
                    bestGap = Mathf.Min(bestGap, lateral);
                }

                if (bestGap < 999f) worstGap = Mathf.Min(worstGap, bestGap);
                Log($"cars #{car.netId} {car.name} pos={car.transform.position:0.0} dir=({dir.x:0.00},{dir.z:0.00}) speed={v.magnitude:0.0} oncomingLateralGap={(bestGap < 999f ? bestGap.ToString("0.0") : "-")}");
            }

            yield return new WaitForSeconds(1f);
        }

        Log($"cars summary: samples={speeds.Count} speed min={(speeds.Count > 0 ? speeds.Min() : 0f):0.0} avg={(speeds.Count > 0 ? speeds.Average() : 0f):0.0} max={(speeds.Count > 0 ? speeds.Max() : 0f):0.0} smallest oncoming lateral gap={(worstGap < 999f ? worstGap.ToString("0.0") : "none")}");
    }

    // Presses "use" for the local player (same method the use key calls) and logs the state / movement flag for a while:
    // after the use animation the player must be Idle again with movement enabled.
    private IEnumerator UseItem(float seconds)
    {
        PlayerStateMachine sm = Sm();
        if (sm == null) { Log("use: no local player"); yield break; }

        PlayerUseHandler use = sm.GetComponentInChildren<PlayerUseHandler>(true);
        if (use == null) use = Me()?.transform.root.GetComponentInChildren<PlayerUseHandler>(true);
        if (use == null) { Log("use: no PlayerUseHandler"); yield break; }

        var handle = typeof(PlayerUseHandler).GetMethod("HandleUse", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var moveField = typeof(PlayerMovement).GetField("isActive", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        handle.Invoke(use, null);

        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < seconds)
        {
            bool moveOn = moveField != null && (bool)moveField.GetValue(sm.movementHandler);
            Log($"use state={sm.currentPlayerState} movementActive={moveOn} carrying={sm.interactionHandler.IsCarrying()}");
            yield return new WaitForSeconds(0.5f);
        }
    }

    // Logs the mixer's slot letters on this machine whenever they change.
    private IEnumerator MixerWatch(float seconds)
    {
        string last = null;
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < seconds)
        {
            MixerUIHelper u = FindAnyObjectByType<MixerUIHelper>(FindObjectsInactive.Include);
            string letters = u == null ? "(no mixer)" : string.Concat(u.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Select(t => "[" + t.text + "]"));
            if (letters != last) { last = letters; Log("mixerwatch " + (NetworkServer.active ? "HOST" : "CLIENT") + " letters=" + letters); }
            yield return new WaitForSeconds(0.3f);
        }
    }

    // The pickup exactly as the interact key does it (HandleInteract: detection -> dispatch -> interact state), with empty
    // hands, N times on the host: a wood plank is spawned in front of the player, picked, checked (hand = handler = server),
    // dropped and removed. Reports every round where the three disagree.
    private IEnumerator RealPick(int rounds, string prefabName)
    {
        PlayerNetwork_MP me = Me();
        if (me == null || !NetworkServer.active) { Log("realpick: needs the host with a local player"); yield break; }

        var interactNet = me.GetComponent<PlayerInteractionNetwork_MP>();
        var handler = me.GetComponent<PlayerInteractionHandler>();
        var press = typeof(PlayerInteractionNetwork_MP).GetMethod("HandleInteract", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var prefab = Resources.Load<GameObject>(prefabName);
        if (prefab == null) prefab = NetworkManager.singleton.spawnPrefabs.FirstOrDefault(g => g != null && g.name == prefabName);
        if (prefab == null) { Log("realpick: prefab not found " + prefabName); yield break; }

        int bad = 0;
        for (int i = 0; i < rounds; i++)
        {
            Vector3 spot = me.transform.position + me.transform.forward * 1.1f + Vector3.up * 0.3f;
            GameObject plank = Instantiate(prefab, spot, Quaternion.identity);
            NetworkServer.Spawn(plank);
            var item = plank.GetComponent<CarriableObject_MP>();
            yield return new WaitForSeconds(0.5f);

            press.Invoke(interactNet, null);
            yield return new WaitForSeconds(0.7f);

            CarriableObject_SP sp = plank.GetComponent<CarriableObject_SP>();
            bool code = handler.GetCurrentCarriable() == sp;
            bool visual = plank.transform.parent == handler.GetCarryTransform();
            bool server = item.Carrier != null && item.Carrier == me.netIdentity;
            if (!(code && visual && server))
            {
                bad++;
                Log($"REALPICK round {i}: code={code} visual={visual} server={server} state={Sm()?.currentPlayerState}");
            }

            if (code) item.NotifyDropped(handler);
            yield return new WaitForSeconds(0.5f);
            NetworkServer.Destroy(plank);
            yield return new WaitForSeconds(0.2f);
        }

        Log($"REALPICK done: {rounds} rounds, {bad} mismatches");
    }

    // Host side of the client test: puts a plank in front of the REMOTE player once per round and removes it afterwards.
    private IEnumerator FeedWood(int rounds)
    {
        var prefab = NetworkManager.singleton.spawnPrefabs.FirstOrDefault(g => g != null && g.name == "Carriable_Wood_Processed_MP");
        if (prefab == null) { Log("feedwood: prefab missing"); yield break; }

        for (int i = 0; i < rounds; i++)
        {
            PlayerNetwork_MP remote = FindObjectsByType<PlayerNetwork_MP>(FindObjectsSortMode.None).FirstOrDefault(p => !p.isOwned);
            if (remote == null) { Log("feedwood: no remote player"); yield break; }

            GameObject plank = Instantiate(prefab, remote.transform.position + remote.transform.forward * 1.1f + Vector3.up * 0.3f, Quaternion.identity);
            NetworkServer.Spawn(plank);

            // Until the client has picked it up and put it down again.
            float t0 = Time.realtimeSinceStartup;
            var item = plank.GetComponent<CarriableObject_MP>();
            while (item.Carrier == null && Time.realtimeSinceStartup - t0 < 4f) yield return null;
            while (item.Carrier != null && Time.realtimeSinceStartup - t0 < 8f) yield return null;

            yield return new WaitForSeconds(0.4f);
            NetworkServer.Destroy(plank);
            yield return new WaitForSeconds(0.3f);
        }

        Log("feedwood done");
    }

    // Client side: the interact key's real path, with empty hands, on planks that the host puts in front of this player.
    private IEnumerator RealPickClient(int rounds)
    {
        PlayerNetwork_MP me = Me();
        if (me == null || NetworkServer.active) { Log("realpickc: needs a pure client with a local player"); yield break; }

        var interactNet = me.GetComponent<PlayerInteractionNetwork_MP>();
        var handler = me.GetComponent<PlayerInteractionHandler>();
        var press = typeof(PlayerInteractionNetwork_MP).GetMethod("HandleInteract", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        int bad = 0, done = 0;
        for (int i = 0; i < rounds; i++)
        {
            CarriableObject_MP item = null;
            for (float t = 0; t < 6f && item == null; t += 0.1f)
            {
                item = FindObjectsByType<CarriableObject_MP>(FindObjectsSortMode.None)
                    .FirstOrDefault(c => c.Carrier == null && c.transform.parent == null && Vector3.Distance(c.transform.position, me.transform.position) < 2.5f);
                if (item == null) yield return new WaitForSeconds(0.1f);
            }
            if (item == null) { Log($"REALPICKC round {i}: no plank appeared"); continue; }

            yield return new WaitForSeconds(0.5f);
            press.Invoke(interactNet, null);
            yield return new WaitForSeconds(0.9f);

            CarriableObject_SP sp = item.GetComponent<CarriableObject_SP>();
            bool code = handler.GetCurrentCarriable() == sp;
            bool visual = item.transform.parent == handler.GetCarryTransform();
            bool server = item.Carrier != null && item.Carrier == me.netIdentity;
            done++;
            if (!(code && visual && server))
            {
                bad++;
                Log($"REALPICKC round {i}: code={code} visual={visual} server={server} state={Sm()?.currentPlayerState}");
            }

            if (code) item.NotifyDropped(handler);
            yield return new WaitForSeconds(1.5f);
        }

        Log($"REALPICKC done: {done} picked rounds of {rounds}, {bad} mismatches");
    }

    // Host orchestrates items that change hands while they are still falling, with both players (host + remote):
    //   0 A drops, B picks it a moment later (item still in the air)      1 A and B pick the same item in the same frame
    //   2 A is stunned while holding it (forced drop), B picks right away   3 A has a full inventory and picks a 4th (swap
    //   drop), B picks the swapped-out item at once.      Server-side calls = what the Cmds end up doing; the other machine
    // (run `inv` there) must agree afterwards. Every round is checked here and, through the watcher, on the client.
    private IEnumerator DropChase(int rounds)
    {
        PlayerNetwork_MP a = Me();
        PlayerNetwork_MP b = FindObjectsByType<PlayerNetwork_MP>(FindObjectsSortMode.None).FirstOrDefault(p => !p.isOwned);
        if (a == null || b == null || !NetworkServer.active) { Log("dropchase: needs host + one remote player"); yield break; }

        PlayerInteractionHandler ha = a.GetComponent<PlayerInteractionHandler>();
        PlayerInteractionHandler hb = b.GetComponent<PlayerInteractionHandler>();
        var prefab = NetworkManager.singleton.spawnPrefabs.FirstOrDefault(g => g != null && g.name == "Carriable_Wood_Processed_MP");
        if (prefab == null) { Log("dropchase: prefab missing"); yield break; }

        float[] delays = { 0f, 0.05f, 0.12f, 0.25f };
        int bad = 0;

        for (int i = 0; i < rounds; i++)
        {
            int kind = i % 4;
            float delay = delays[(i / 4) % delays.Length];
            var spawned = new List<GameObject>();
            Func<CarriableObject_MP> make = () =>
            {
                GameObject g = Instantiate(prefab, a.transform.position + a.transform.forward * 1.2f + Vector3.up * 1.2f, Quaternion.identity);
                NetworkServer.Spawn(g);
                spawned.Add(g);
                return g.GetComponent<CarriableObject_MP>();
            };

            CarriableObject_MP item = make();
            yield return new WaitForSeconds(0.3f);

            if (kind == 0)
            {
                item.NotifyPickedUp(ha);
                yield return new WaitForSeconds(0.4f);
                item.NotifyDropped(ha);
                yield return new WaitForSeconds(delay);
                item.NotifyPickedUp(hb);
            }
            else if (kind == 1)
            {
                item.NotifyPickedUp(ha);
                item.NotifyPickedUp(hb);
            }
            else if (kind == 2)
            {
                item.NotifyPickedUp(ha);
                yield return new WaitForSeconds(0.4f);
                a.RequestStun();
                yield return new WaitForSeconds(0.1f + delay);
                item.NotifyPickedUp(hb);
            }
            else
            {
                var held = new List<CarriableObject_MP> { item };
                item.NotifyPickedUp(ha);
                for (int k = 0; k < PlayerInteractionHandler.InventorySize - 1; k++)
                {
                    yield return new WaitForSeconds(0.3f);
                    CarriableObject_MP extra = make();
                    yield return new WaitForSeconds(0.2f);
                    held.Add(extra);
                    extra.NotifyPickedUp(ha);
                }
                yield return new WaitForSeconds(0.4f);
                CarriableObject_MP fourth = make();
                yield return new WaitForSeconds(0.2f);
                CarriableObject_SP before = ha.GetCurrentCarriable();
                fourth.NotifyPickedUp(ha);
                yield return new WaitForSeconds(delay);
                // the one that was swapped out of the hand
                CarriableObject_MP swapped = before != null ? before.GetComponent<CarriableObject_MP>() : null;
                if (swapped != null) swapped.NotifyPickedUp(hb);
            }

            yield return new WaitForSeconds(1.3f);

            // Host's own view of every plank: hand, handler and server must agree.
            foreach (GameObject g in spawned)
            {
                if (g == null) continue;
                var c = g.GetComponent<CarriableObject_MP>();
                var sp = g.GetComponent<CarriableObject_SP>();
                PlayerInteractionHandler handHolder = new[] { ha, hb }.FirstOrDefault(h => h.GetCarryTransform() != null && g.transform.parent == h.GetCarryTransform());
                PlayerInteractionHandler codeHolder = new[] { ha, hb }.FirstOrDefault(h => h.GetCurrentCarriable() == sp ||
                    Enumerable.Range(0, PlayerInteractionHandler.InventorySize).Any(n => h.GetSlotItem(n) == sp));
                bool serverHeld = c.Carrier != null;
                if ((handHolder != null) != (codeHolder != null) || (handHolder != codeHolder) || (serverHeld != (handHolder != null)))
                {
                    bad++;
                    Log($"DROPCHASE round {i} kind={kind} delay={delay}: #{c.netId} hand={(handHolder != null ? handHolder.transform.root.name : "-")} code={(codeHolder != null ? codeHolder.transform.root.name : "-")} server={(c.Carrier != null ? c.Carrier.name : "-")}");
                }
            }

            yield return new WaitForSeconds(a.isDead || Sm()?.currentPlayerState == PlayerStates.Stunned ? 3.5f : 0.2f);

            foreach (GameObject g in spawned)
            {
                if (g == null) continue;
                var c = g.GetComponent<CarriableObject_MP>();
                if (c.Carrier != null) { var h = c.Carrier == a.netIdentity ? ha : hb; c.NotifyDropped(h); }
            }
            yield return new WaitForSeconds(0.5f);
            foreach (GameObject g in spawned) if (g != null) NetworkServer.Destroy(g);
            yield return new WaitForSeconds(0.3f);
        }

        Log($"DROPCHASE done: {rounds} rounds, {bad} host-side mismatches");
    }

    private IEnumerator CarWatch(float seconds)
    {
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < seconds)
        {
            PlayerNetwork_MP[] players = FindObjectsByType<PlayerNetwork_MP>(FindObjectsSortMode.None);

            foreach (MpCarHitSync_MP car in FindObjectsByType<MpCarHitSync_MP>(FindObjectsSortMode.None))
            {
                PlayerNetwork_MP nearest = players.OrderBy(p => Dist(p.transform, car.transform)).FirstOrDefault();
                float d = nearest != null ? Dist(nearest.transform, car.transform) : -1f;
                if (nearest == null || d > 12f) continue;

                Rigidbody rb = car.GetComponent<Rigidbody>();
                Rigidbody nrb = nearest.GetComponent<Rigidbody>();
                PlayerStateMachine nsm = nearest.GetComponent<PlayerStateMachine>();

                Log($"carwatch car#{car.netId} pos={car.transform.position:0.0} speed={(rb != null ? rb.linearVelocity.magnitude : -1f):0.0} " +
                    $"kin={(rb != null && rb.isKinematic)} | player#{nearest.netId} {(nearest.isOwned ? "ME" : "remote")} dist={d:0.0} " +
                    $"kin={(nrb != null && nrb.isKinematic)} state={nsm?.currentPlayerState} dead={nearest.isDead}");
            }

            yield return new WaitForSeconds(0.25f);
        }
    }

    // Logs where each player's ragdoll hips are (and whether the active ragdoll runs) every 0.5 s: compare the owner's
    // numbers with the other machine's view of the same player.
    private IEnumerator RagWatch(float seconds)
    {
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < seconds)
        {
            foreach (PlayerNetwork_MP p in FindObjectsByType<PlayerNetwork_MP>(FindObjectsSortMode.None))
            {
                Rigidbody hips = p.RagdollHips;
                PlayerStateMachine sm = p.GetComponent<PlayerStateMachine>();
                Animator anim = sm != null ? sm.animator : null;
                SkinnedMeshRenderer smr = anim != null ? anim.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
                string vis = smr != null ? $" smr={(smr.enabled && smr.gameObject.activeInHierarchy ? "on" : "off")} h={smr.bounds.size.y:0.00} cy={(smr.bounds.center.y - p.transform.position.y):0.00} tilt={(anim != null ? anim.transform.up.y : 0f):0.00}" : " smr=none";
                Log($"ragwatch t={Time.realtimeSinceStartup - t0:0.0} player#{p.netId} {(p.isOwned ? "ME" : "remote")} state={sm?.currentPlayerState} " +
                    $"animator={(anim != null && anim.enabled ? AnimName(anim) : "OFF")} " +
                    $"activeRagdoll={p.ActiveRagdollRunning}{vis} hips={(hips != null ? hips.position.ToString("0.00") : "-")} hipsKin={(hips != null && hips.isKinematic)} root={p.transform.position.ToString("0.00")}");
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    // Where every player's package, dog house, food bowl and mailbox stand (do the homes overlap?).
    private void LogHomes()
    {
        foreach (PlayerNetwork_MP p in FindObjectsByType<PlayerNetwork_MP>(FindObjectsSortMode.None))
        {
            Transform root = p.transform.root;
            string Pos(string name)
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.position.ToString("0.0");
                return "-";
            }

            Log($"home player#{p.netId} {(p.isOwned ? "ME" : "remote")} root={root.position:0.0} body={p.transform.position:0.0} " +
                $"DogHouse={Pos("DogHouse")} FoodBowl={Pos("FoodBowl")} MailBox={Pos("MailBox")} MailManPackage={Pos("MailManPackage")} Dog={Pos("Dog")}");
        }
    }

    // Hazard states on this machine: warning phase, repairable, "!" badge shown.
    private void LogHazards()
    {
        var bf = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (TelegraphedHazard h in FindObjectsByType<TelegraphedHazard>(FindObjectsSortMode.None))
        {
            var indicator = typeof(TelegraphedHazard).GetField("indicator", bf)?.GetValue(h) as WorldIndicator;
            Log($"hazard '{h.name}' {h.GetType().Name} state={typeof(TelegraphedHazard).GetField("state", bf)?.GetValue(h)} " +
                $"repairable={h.IsRepairable} badge={(indicator != null && indicator.IsShown)} netId={h.GetComponent<NetworkIdentity>()?.netId}");
        }
    }

    // Presses "repair" on the first hazard that is repairable here, through the real proxy path (server decides).
    private void RepairHazard()
    {
        foreach (Hazard_MP h in FindObjectsByType<Hazard_MP>(FindObjectsSortMode.None))
        {
            TelegraphedHazard hazard = h.GetComponent<TelegraphedHazard>();
            if (hazard == null || !hazard.IsRepairable) continue;

            h.RequestInteract(NetworkClient.localPlayer, null);
            Log($"repair requested on '{h.name}'");
            return;
        }

        Log("repair: nothing repairable");
    }

    private IEnumerator Car(float timeout)
    {
        float t0 = Time.realtimeSinceStartup;
        MpCarHitSync_MP car = null;
        while (car == null && Time.realtimeSinceStartup - t0 < timeout)
        {
            car = FindObjectsByType<MpCarHitSync_MP>(FindObjectsSortMode.None).OrderByDescending(x => x.netId).FirstOrDefault(); // newest car: every machine picks the same one
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



    // Lists every active transform whose name contains the text (scene duplicates hunt): name, world position, parent,
    // networked or not. Run it on host and client and compare the counts.
    private void LogObjects(string needle)
    {
        int n = 0;
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).OrderBy(x => x.name))
        {
            if (t.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0) continue;
            NetworkIdentity id = t.GetComponentInParent<NetworkIdentity>();
            Log($"obj '{t.name}' pos={t.position:0.00} parent='{(t.parent ? t.parent.name : "-")}' root='{t.root.name}' net={(id ? "#" + id.netId + (id.sceneId != 0 ? " scene" : " spawned") : "-")}");
            n++;
        }
        Log($"objs '{needle}': {n} found ({(NetworkServer.active ? "HOST" : "CLIENT")})");
    }




    // Waits until the shared network clock hits a multiple of the period: host and client then act within a few ms.
    private static IEnumerator SyncWait(float period)
    {
        while (true)
        {
            double phase = NetworkTime.time % period;
            if (phase < 0.03) yield break;
            yield return null;
        }
    }

    private IEnumerator RaceRun(int rounds)
    {
        for (int i = 0; i < rounds; i++)
        {
            yield return SyncWait(7f);
            yield return RaceTake();
            yield return new WaitForSeconds(1.0f);
            yield return Drop();
            yield return new WaitForSeconds(1.5f);
        }
    }

    // Everybody grabs the SAME item (lowest free processed one) at the same instant: the server must give it to one
    // player only and the loser must end up with an empty hand, in code and visually.
    private IEnumerator RaceTake()
    {
        CarriableObject_MP item = FindObjectsByType<CarriableObject_MP>(FindObjectsSortMode.None)
            .Where(x => x.transform.parent == null && x.Carrier == null && !x.GetComponent<CarriableObject_SP>().isRawMaterial)
            .OrderBy(x => x.netId).FirstOrDefault();
        if (item == null) { Log("racetake: no free item"); yield break; }

        PlayerInteractionHandler h = Handler();
        if (h != null && h.GetCurrentCarriable() != null) { Log("racetake: hands not empty"); yield break; }

        item.NotifyPickedUp(h);
        yield return new WaitForSeconds(1.2f);
        Log($"racetake #{item.netId}: I hold={HeldName()} carrier={(item.Carrier ? item.Carrier.netId.ToString() : "-")} mine={(item.Carrier != null && item.Carrier == NetworkClient.localPlayer)}");
    }

    // ---- API-level actions: same network path as the player's key press (what DispatchInteract does after the
    // target was detected), without the walking / detection heuristics that make scripted tests flaky ----
    private static void DispatchLikePlayer(Component target)
    {
        PlayerInteractionHandler h = Handler();
        if (target == null || h == null) return;

        if (target.TryGetComponent(out CarriableObject_MP item)) { item.NotifyPickedUp(h); return; }

        IInteractionNetworkProxy proxy = target.GetComponentInParent<IInteractionNetworkProxy>();
        if (proxy != null) { proxy.RequestInteract(NetworkClient.localPlayer, target); return; }

        Me()?.GetComponent<PlayerInteractRouter_MP>()?.RequestInteract(target);
    }

    private IEnumerator TakeApi(string nameFilter)
    {
        Transform me = Body();
        CarriableObject_MP item = FindObjectsByType<CarriableObject_MP>(FindObjectsSortMode.None)
            .Where(x => x.transform.parent == null && x.Carrier == null &&
                        !x.GetComponent<CarriableObject_SP>().isRawMaterial &&
                        (string.IsNullOrEmpty(nameFilter) || x.name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0))
            .OrderBy(x => Dist(me, x.transform)).FirstOrDefault();
        if (item == null) { Log("takeapi: no free item"); yield break; }

        DispatchLikePlayer(item);
        yield return new WaitForSeconds(0.8f);
        Log($"takeapi #{item.netId} {item.name}: holding={HeldName()} parent={(item.transform.parent ? item.transform.parent.name : "-")} carrier={(item.Carrier ? item.Carrier.netId.ToString() : "-")}");
    }

    private IEnumerator BuildApi()
    {
        Transform me = Body();
        CarriableObject_SP held = Handler()?.GetCurrentCarriable();
        if (held == null) { Log("buildapi: not holding anything"); yield break; }

        Constructor target = FindObjectsByType<Constructor>(FindObjectsSortMode.None)
            .Where(k => !k.IsBuilt() && k.GetAcceptedType() == held.carriableType)
            .OrderBy(k => Dist(me, k.transform)).FirstOrDefault();
        if (target == null) { Log($"buildapi: no free constructor for {held.carriableType}"); yield break; }

        DispatchLikePlayer(target);
        yield return new WaitForSeconds(1.0f);
        lastBuilt = target;
        Log($"buildapi '{target.name}': built={target.IsBuilt()} holding={HeldName()}");
    }

    private IEnumerator HammerApi(int hits)
    {
        if (lastBuilt == null) { Log("hammerapi: build first"); yield break; }
        Component tile = lastBuilt.GetComponentsInChildren<Component>(false).FirstOrDefault(x => x is WoodTile || x is BrickTile || x is GlassTile);
        if (tile == null) { Log("hammerapi: no active tile"); yield break; }

        for (int i = 0; i < hits && !TileDone(tile); i++)
        {
            DispatchLikePlayer(tile);
            yield return new WaitForSeconds(0.35f);
        }

        Log($"hammerapi tile '{tile.name}' complete={TileDone(tile)}");
    }

    // Which chat does the T hotkey open, and does opening it touch another player's camera / HUD?
    private void ChatOpenProbe()
    {
        var hotkey = FindAnyObjectByType<MpChatHotkey_MP>(FindObjectsInactive.Include);
        var find = typeof(MpChatHotkey_MP).GetMethod("FindChat", BindingFlags.Instance | BindingFlags.NonPublic);
        ChatUI chat = hotkey != null ? find.Invoke(hotkey, null) as ChatUI : null;

        NetworkIdentity package = chat != null ? chat.GetComponentInParent<NetworkIdentity>(true) : null;
        Log($"chatopen: chat={(chat != null ? chat.transform.root.name : "none")} package#{(package != null ? package.netId.ToString() : "-")} owned={(package != null && package.isOwned)}");

        if (chat == null) return;
        chat.Open();

        foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            NetworkIdentity owner = cam.GetComponentInParent<NetworkIdentity>(true);
            Log($"  camera '{cam.name}' enabled={cam.enabled} active={cam.gameObject.activeInHierarchy} package#{(owner != null ? owner.netId.ToString() : "-")} owned={(owner != null && owner.isOwned)}");
        }

        chat.Close();
    }

    // The dogs' animator state on this machine: compare host and client for the same dog.
    private void LogDogs()
    {
        foreach (SkinnedMeshRenderer rd in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
        {
            if (rd.name != "GoldenDog_LP" || !rd.gameObject.activeInHierarchy) continue;

            Animator animator = rd.GetComponentInParent<Animator>(true);
            NetworkIdentity owner = rd.GetComponentInParent<NetworkIdentity>(true);
            AnimatorStateInfo info = animator != null ? animator.GetCurrentAnimatorStateInfo(0) : default;
            Log($"dog package#{(owner != null ? owner.netId.ToString() : "-")} {(owner != null && owner.isOwned ? "MINE" : "other")} animator={(animator != null ? animator.name : "NONE")} state={info.fullPathHash} time={info.normalizedTime:0.00}");
        }
    }

    // Code <-> visual consistency of every carriable on THIS machine, twice a second. A violation has to persist for two
    // samples (RPCs apply a frame late) before it is logged. Cases: plank sits in a hand but no handler holds it (visual
    // without code), a handler holds it but it sits elsewhere (code without visual), holder differs from the server's carrier.
    private IEnumerator InvariantWatch(float seconds)
    {
        var pending = new Dictionary<string, int>();
        float t0 = Time.realtimeSinceStartup;
        int samples = 0, logged = 0;
        while (Time.realtimeSinceStartup - t0 < seconds)
        {
            samples++;
            PlayerInteractionHandler[] handlers = FindObjectsByType<PlayerInteractionHandler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var now = new HashSet<string>();

            foreach (CarriableObject_MP c in FindObjectsByType<CarriableObject_MP>(FindObjectsSortMode.None))
            {
                CarriableObject_SP sp = c.GetComponent<CarriableObject_SP>();
                PlayerInteractionHandler handHolder = handlers.FirstOrDefault(h => h.GetCarryTransform() != null && c.transform.parent == h.GetCarryTransform());
                PlayerInteractionHandler codeHolder = handlers.FirstOrDefault(h => h.GetCurrentCarriable() == sp ||
                    Enumerable.Range(0, PlayerInteractionHandler.InventorySize).Any(i => h.GetSlotItem(i) == sp));
                NetworkIdentity carrier = c.Carrier;
                string problem = null;

                if (handHolder != null && codeHolder == null) problem = "VISUAL-NO-CODE (in hand of " + handHolder.transform.root.name + ", no handler holds it)";
                else if (handHolder == null && codeHolder != null) problem = "CODE-NO-VISUAL (handler of " + codeHolder.transform.root.name + " holds it, parent=" + (c.transform.parent ? c.transform.parent.name : "-") + ")";
                else if (handHolder != null && codeHolder != null && handHolder != codeHolder) problem = "HOLDER-MISMATCH hand=" + handHolder.transform.root.name + " code=" + codeHolder.transform.root.name;
                else if (carrier == null && (handHolder != null || codeHolder != null)) problem = "SERVER-FREE but held locally";
                else if (carrier != null && handHolder == null && codeHolder == null) problem = "SERVER-CARRIER=" + carrier.netId + " but nobody holds it here, parent=" + (c.transform.parent ? c.transform.parent.name : "-");
                else if (carrier != null && handHolder != null && handHolder.GetComponentInParent<NetworkIdentity>() != carrier) problem = "CARRIER-MISMATCH server=" + carrier.netId + " hand=" + handHolder.transform.root.name;

                if (problem == null) continue;
                string key = c.netId + ":" + problem;
                now.Add(key);
                pending[key] = pending.TryGetValue(key, out int n) ? n + 1 : 1;
                if (pending[key] == 2) { logged++; Log($"INV #{c.netId} {c.name}: {problem}"); }
            }

            foreach (string k in pending.Keys.Where(k => !now.Contains(k)).ToList()) pending.Remove(k);
            yield return new WaitForSeconds(0.5f);
        }
        Log($"INV done: {samples} samples, {logged} violations");
    }

    // ------------------------------------------------------------------ report

    // mpmenu state|multiplayer|back: drives the merged menu like a click and logs which panels are on.
    private static void MpMenuProbe(string what)
    {
        UnityEngine.UI.Button Find(string name, string parent)
        {
            foreach (var b in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.name == name && (parent == null || (b.transform.parent != null && b.transform.parent.name == parent))) return b;
            return null;
        }

        if (what == "multiplayer") Find("Button_Multiplayer", "Buttons_MainMenu")?.onClick.Invoke();
        else if (what == "back") Find("Button_BackToMenu", "Buttons_AfterMultiplayer")?.onClick.Invoke();

        string State(string n)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.name == n && t.gameObject.scene.IsValid()) return t.gameObject.activeInHierarchy ? "ON" : "off";
            return "missing";
        }

        Log($"mpmenu {what}: MainMenu={State("MainMenu")} MainMenu_MP={State("MainMenu_MP")} Buttons_AfterMultiplayer={State("Buttons_AfterMultiplayer")} Panel_Lobby={State("Panel_Lobby")} " +
            $"server={NetworkServer.active} client={NetworkClient.active} scene={SceneManager.GetActiveScene().name}");
    }

    private static string BodyMesh(PlayerNetwork_MP p)
    {
        foreach (SkinnedMeshRenderer r in p.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (r.transform.parent != null && r.transform.parent.name == "Character_01_Male")
                return r.sharedMesh != null ? r.sharedMesh.name + "/" + r.sharedMesh.vertexCount : "nomesh";
        return "nobody";
    }

    private void Report()
    {
        string scene = SceneManager.GetActiveScene().name;
        Log($"--- report ({(NetworkServer.active ? "HOST" : "CLIENT")}) scene={scene} connected={NetworkClient.isConnected} " +
            $"rtt={NetworkTime.rtt * 1000:0}ms spawned={NetworkClient.spawned.Count} errors={errors} exceptions={exceptions} t={Time.time:F1} frame={Time.frameCount} ts={Time.timeScale}");

        foreach (PlayerNetwork_MP p in FindObjectsByType<PlayerNetwork_MP>(FindObjectsSortMode.None))
        {
            Animator a = p.GetComponentInChildren<Animator>();
            PlayerStateMachine sm = p.GetComponent<PlayerStateMachine>();
            PlayerInteractionHandler h = p.GetComponent<PlayerInteractionHandler>();
            Log($"  player #{p.netId} {(p.isOwned ? "ME" : "remote")} state={sm?.currentPlayerState} anim={AnimName(a)} " +
                $"carrying={(h != null && h.IsCarrying())} item={(h?.GetCurrentCarriable() ? h.GetCurrentCarriable().name : "-")} " +
                $"inHand=[{string.Join(",", (h != null && h.GetCarryTransform() != null ? h.GetCarryTransform().GetComponentsInChildren<CarriableObject_MP>(true) : new CarriableObject_MP[0]).Select(x => x.name + "#" + x.netId))}] " +
                $"slots=[{string.Join(",", Enumerable.Range(0, PlayerInteractionHandler.InventorySize).Select(i => h != null && h.GetSlotItem(i) != null ? h.GetSlotItem(i).name.Replace("Carriable_", "").Replace("(Clone)", "") : "-"))}] active={(h != null ? h.GetActiveSlot() : -1)} " +
                $"name='{p.playerName}' color={p.colorIndex} char={p.characterIndex} body={BodyMesh(p)} micMuted={p.micMuted} dead={p.isDead} pos={p.transform.position}");
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
