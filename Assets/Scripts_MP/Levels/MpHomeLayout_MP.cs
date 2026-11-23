using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Every player package brings its own dog house, food bowl and mailbox, and the packages spawn on a 1.2 m circle
// around the level's authored start: the homes of 2-4 players stood on top of each other. This lines them up side by
// side, starting at the homes the level designer placed (the scene's authored package) and following the house's
// own "right" axis. The direction (+ or -) is the one with more free space (static colliders only, so every machine
// picks the same). Slot 0 keeps the authored spot; slot 1-3 follow at 2 m (houses) / 1.2 m (mailboxes).
public static class MpHomeLayout_MP
{
    private const int MaxSlots = 4;
    private const float SpawnRadius = 1.2f; // LobbyNetworkManager.OnServerAddPlayer separation
    private const float DogHouseStep = 2f;
    private const float MailBoxStep = 2f;
    private const float TightFactor = 0.6f; // used when a full step has no room on either side
    private const float MinObstacleHeight = 0.3f; // lower than this = ground, kerb, ramp: walkable

    // NB: not "plane"/"uçak": the wooden fences are meshes called "Uçak.006" (Plane.006).
    private static readonly string[] IgnoredNames = { "ground", "terrain", "zone", "patrol", "bound" };

    private struct Row
    {
        public Vector3 direction;
        public float step;
    }

    private static int cachedScene = -1;
    private static Row dogRow;
    private static Row mailRow;

    // ownsMail: this package keeps the level's single mailbox and mail man (the host's). Every other package has
    // its MailManPackage switched off on all machines, so there is one mailbox and one mail man for everybody.
    public static void Apply(Transform package, bool ownsMail)
    {
        if (package == null) return;

        // Compete keeps every side's own dog, house and bowl exactly where its scene put them.
        bool competing = VersusMatch.Instance != null;

        if (!ownsMail)
        {
            Transform otherMail = FindDeep(package, "MailManPackage");
            if (otherMail != null && otherMail.gameObject.activeSelf) otherMail.gameObject.SetActive(false);

            // Together: ONE dog for everybody (the host's). The other players' dog, house, bed and bowl are off on every
            // machine, and their dog panel with them (it would show a dog that is not there).
            if (!competing)
            {
                Transform otherDogs = FindDeep(package, "DogPackage");
                if (otherDogs != null && otherDogs.gameObject.activeSelf) otherDogs.gameObject.SetActive(false);

                Transform dogPanel = FindDeep(package, "DogUI");
                if (dogPanel != null && dogPanel.gameObject.activeSelf) dogPanel.gameObject.SetActive(false);
            }
        }

        // Compete: the scene's own match code (VersusDogGuard) moves each side's whole dog package to its dog anchor. The
        // rows below would put the house, bed and bowl somewhere else.
        if (competing) return;

        if (!MpLevelInstaller.HasAuthoredSpawn) return;

        Transform authored = FindAuthoredPackage();
        if (authored == null) return;

        Vector3 offset = package.position - MpLevelInstaller.AuthoredSpawnPosition;
        offset.y = 0f;
        if (Mathf.Abs(offset.magnitude - SpawnRadius) > 0.5f) return; // not on the spawn circle: leave it alone

        int slot = Mathf.RoundToInt(Mathf.Atan2(offset.z, offset.x) / (Mathf.PI * 2f / MaxSlots));
        slot = ((slot % MaxSlots) + MaxSlots) % MaxSlots;

        Transform authoredHouse = FindDeep(authored, "DogHouse");
        Transform authoredMailBox = FindDeep(authored, "MailBox");

        int sceneHandle = SceneManager.GetActiveScene().handle;
        if (cachedScene != sceneHandle)
        {
            cachedScene = sceneHandle;
            dogRow = authoredHouse != null ? ChooseRow(authoredHouse, authored, DogHouseStep) : default;
            mailRow = authoredMailBox != null ? ChooseRow(authoredMailBox, authored, MailBoxStep) : default;

            InteractionDebug.Log($"[MP-HOME] rows: dog houses dir {dogRow.direction:0} step {dogRow.step:0.0}, " +
                                 $"mailboxes dir {mailRow.direction:0} step {mailRow.step:0.0}.");
        }

        Vector3 dogShift = dogRow.direction * (slot * dogRow.step);
        // The one mailbox stands where the level designer put it.
        Vector3 mailShift = ownsMail ? Vector3.zero : mailRow.direction * (slot * mailRow.step);

        if (ownsMail) AlignMailMan(package, authored);

        // Together: the one dog house (the host's) stands where the level designer put it.
        Place(package, authored, "DogHouse", competing ? dogShift : Vector3.zero);
        Place(package, authored, "BedTransform", competing ? dogShift : Vector3.zero);
        Place(package, authored, "FoodBowl", competing ? dogShift : Vector3.zero);
        Place(package, authored, "MailBox", mailShift);

        InteractionDebug.Log($"[MP-HOME] '{package.name}' slot {slot}: dog house shifted {dogShift:0.0}, mailbox shifted {mailShift:0.0}.");
    }

    private static readonly System.Reflection.BindingFlags NonPublicInstance =
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

    // The mail man walks the points of his own MailManPackage, which sits on the spawn circle with the rest of the player
    // package, while the single mailbox stands where the level designer put it. He delivered next to his package, far
    // from the box. Put his package where the authored one is, and give him the right mailbox and start point (both were
    // taken in Awake, before any of this: the first MailBox found, and the spawn position of the old place).
    private static void AlignMailMan(Transform package, Transform authored)
    {
        Transform own = FindDeep(package, "MailManPackage");
        Transform reference = FindDeep(authored, "MailManPackage");
        if (own == null || reference == null) return;

        own.SetPositionAndRotation(reference.position, reference.rotation);

        MailManController controller = own.GetComponentInChildren<MailManController>(true);
        if (controller == null) return;

        typeof(MailManController).GetField("spawnPosition", NonPublicInstance)?.SetValue(controller, controller.transform.position);

        Transform box = FindDeep(package, "MailBox");
        MailBox mailBox = box != null ? box.GetComponent<MailBox>() : null;
        if (mailBox != null) typeof(MailManController).GetField("mailBox", NonPublicInstance)?.SetValue(controller, mailBox);

        InteractionDebug.Log($"[MP-HOME] mail man package aligned to the authored one at {reference.position:0.0}.");
    }

    private static void Place(Transform package, Transform authored, string objectName, Vector3 shift)
    {
        Transform own = FindDeep(package, objectName);
        Transform reference = FindDeep(authored, objectName);
        if (own == null || reference == null) return;

        own.SetPositionAndRotation(reference.position + shift, reference.rotation);
    }

    // Best (direction, step): the most free slots; ties prefer the full step, then +right.
    private static Row ChooseRow(Transform home, Transform authored, float step)
    {
        Vector3 right = home.right;
        right.y = 0f;
        right.Normalize();

        Row best = default;
        int bestFree = -1;

        foreach (float candidateStep in new[] { step, step * TightFactor })
        {
            foreach (Vector3 direction in new[] { right, -right })
            {
                int free = FreeSlots(home.position, direction, candidateStep, authored);
                if (free <= bestFree) continue;

                bestFree = free;
                best = new Row { direction = direction, step = candidateStep };
            }

            if (bestFree >= MaxSlots - 1) break; // room for every slot at this step
        }

        return best;
    }

    private static int FreeSlots(Vector3 origin, Vector3 direction, float step, Transform authored)
    {
        int free = 0;

        for (int slot = 1; slot < MaxSlots; slot++)
        {
            // The box spans from the previous slot to this one, so a fence between two houses counts too.
            Vector3 center = origin + direction * step * (slot - 0.5f) + Vector3.up * 0.4f;
            Collider[] hits = Physics.OverlapBox(center, new Vector3(0.7f, 0.5f, step * 0.5f + 0.3f),
                Quaternion.LookRotation(direction), ~0, QueryTriggerInteraction.Ignore);

            if (IsBlocked(hits, authored)) break;
            free++;
        }

        return free;
    }

    // Only static, tall scenery counts: cars, players and carried items differ per machine; ground is walkable.
    private static bool IsBlocked(Collider[] hits, Transform authored)
    {
        foreach (Collider hit in hits)
        {
            if (hit == null || hit.attachedRigidbody != null || hit is TerrainCollider) continue;
            if (hit.bounds.size.y < MinObstacleHeight) continue;
            if (hit.transform.IsChildOf(authored) || hit.transform.root.name.StartsWith("Player_Package")) continue;

            string lower = hit.name.ToLowerInvariant();
            bool ignored = false;
            foreach (string part in IgnoredNames)
            {
                if (!lower.Contains(part)) continue;
                ignored = true;
                break;
            }

            if (!ignored) return true;
        }

        return false;
    }

    private static Transform FindAuthoredPackage()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == "Player_Package_Default") return root.transform;

        return null;
    }

    private static Transform FindDeep(Transform root, string objectName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == objectName) return t;

        return null;
    }
}
