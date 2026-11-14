using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Applies the active ragdoll setup to the live player prefab (Player_Package_Default).
// The pre-ragdoll player is kept as Player_Package_Default_Old. If the live prefab is missing it is copied from
// the old one; otherwise it is updated in place (never deleted, so its GUID and all scene links survive).
//
// Fixes found in the original rig:
//  - every ConfigurableJoint had angular X/Y/Z = Free with 0 limits (limbs could spin freely),
//  - no forearm bodies (hands were jointed straight to the upper arm, so elbows could not bend physically).
//
// Joint frames are built from the rig's geometry: primary axis = the bend hinge (cross of bone direction and
// bend direction), secondary axis = along the bone. ConfigurableJoint reports a positive rotation about its
// axis as a negative X angle, so the main bend uses the low X limit.
public static class ActiveRagdollSetup
{
    private const string SourcePath = "Assets/Prefabs/Player/Player_Package_Default_Old.prefab";
    private const string TargetPath = "Assets/Prefabs/Player/Player_Package_Default.prefab";
    private const string Prefix = "mixamorig:";

    private struct Spec
    {
        public string bone, parent, child;
        public float mass;
        public int flexDir;   // 0 forward, 1 back
        public float flex, ext, twist, swing;
        public Spec(string bone, string parent, string child, float mass, int flexDir, float flex, float ext, float twist, float swing)
        {
            this.bone = bone; this.parent = parent; this.child = child; this.mass = mass; this.flexDir = flexDir;
            this.flex = flex; this.ext = ext; this.twist = twist; this.swing = swing;
        }
    }

    // bone, connected body, child bone (for direction), mass, bend direction, bend, back-bend, twist along bone, side swing
    private static readonly Spec[] Specs =
    {
        new Spec("Hips",         null,          "Spine",        1.0f,  0,   0,  0,  0,   0),
        new Spec("Spine",        "Hips",        "Spine1",       0.8f,  0,  45, 30, 30,  30),
        new Spec("Head",         "Spine",       "HeadTop_End",  0.3f,  0,  60, 50, 70,  40),
        new Spec("LeftUpLeg",    "Hips",        "LeftLeg",      0.35f, 0, 110, 30, 35,  45),
        new Spec("LeftLeg",      "LeftUpLeg",   "LeftFoot",     0.2f,  1, 140,  5, 10,   5),
        new Spec("RightUpLeg",   "Hips",        "RightLeg",     0.35f, 0, 110, 30, 35,  45),
        new Spec("RightLeg",     "RightUpLeg",  "RightFoot",    0.2f,  1, 140,  5, 10,   5),
        new Spec("LeftArm",      "Spine",       "LeftForeArm",  0.2f,  0, 150, 60, 90, 110),
        new Spec("LeftForeArm",  "LeftArm",     "LeftHand",     0.15f, 0, 150,  5, 90,  10),
        new Spec("LeftHand",     "LeftForeArm", "LeftHandMiddle1",  0.08f, 0, 70, 70, 40, 40),
        new Spec("RightArm",     "Spine",       "RightForeArm", 0.2f,  0, 150, 60, 90, 110),
        new Spec("RightForeArm", "RightArm",    "RightHand",    0.15f, 0, 150,  5, 90,  10),
        new Spec("RightHand",    "RightForeArm","RightHandMiddle1", 0.08f, 0, 70, 70, 40, 40),
    };

    [MenuItem("Tools/Active Ragdoll/Apply To Player Prefab")]
    private static void ApplyToPlayerPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath) == null)
        {
            if (!AssetDatabase.CopyAsset(SourcePath, TargetPath))
            {
                Debug.LogError("[ActiveRagdollSetup] Could not copy " + SourcePath);
                return;
            }
        }
        else if (!EditorUtility.DisplayDialog("Active Ragdoll",
                     "Re-apply joint limits, bodies and masses to " + TargetPath + "? Manual joint tweaks on it will be overwritten.", "Apply", "Cancel"))
            return;

        var root = PrefabUtility.LoadPrefabContents(TargetPath);
        try
        {
            if (Build(root)) PrefabUtility.SaveAsPrefabAsset(root, TargetPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath);
    }

    private static bool Build(GameObject root)
    {
        var sm = root.GetComponentInChildren<PlayerStateMachine>(true);
        if (sm == null || sm.animator == null)
        {
            Debug.LogError("[ActiveRagdollSetup] PlayerStateMachine/Animator not found in the copied prefab.");
            return false;
        }

        Transform model = sm.animator.transform;
        Vector3 fwd = sm.transform.forward;
        var bodies = new Dictionary<string, Rigidbody>();
        var log = new System.Text.StringBuilder("[ActiveRagdollSetup] Built " + TargetPath + "\n");

        // Bodies + colliders first, so joints can connect to them.
        foreach (var s in Specs)
        {
            Transform t = Find(model, Prefix + s.bone);
            if (t == null)
            {
                Debug.LogError("[ActiveRagdollSetup] Bone not found: " + Prefix + s.bone);
                return false;
            }

            var rb = t.GetComponent<Rigidbody>();
            if (rb == null) rb = t.gameObject.AddComponent<Rigidbody>();
            rb.mass = s.mass;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            bodies[s.bone] = rb;

            if (t.GetComponent<Collider>() == null)
            {
                Transform child = Find(t, Prefix + s.child);
                var box = t.gameObject.AddComponent<BoxCollider>();
                Vector3 local = child != null ? t.InverseTransformPoint(child.position) : new Vector3(0f, 0.2f, 0f);
                int axis = DominantAxis(local);
                Vector3 size = Vector3.one * 0.08f;
                size[axis] = Mathf.Abs(local[axis]) * 0.95f;
                box.size = size;
                box.center = local * 0.5f;
                log.AppendLine("  + collider on " + s.bone + " size " + size.ToString("F2"));
            }
        }

        foreach (var s in Specs)
        {
            Transform t = bodies[s.bone].transform;
            var joint = t.GetComponent<ConfigurableJoint>();
            if (s.parent == null)
            {
                if (joint != null) Object.DestroyImmediate(joint);
                continue;
            }
            if (joint == null) joint = t.gameObject.AddComponent<ConfigurableJoint>();

            Transform child = Find(t, Prefix + s.child);
            Vector3 boneDir = child != null ? (child.position - t.position).normalized : (t.position - t.parent.position).normalized;
            Vector3 bendDir = s.flexDir == 0 ? fwd : -fwd;
            Vector3 hinge = Vector3.Cross(boneDir, bendDir);
            if (hinge.sqrMagnitude < 1e-4f) hinge = Vector3.Cross(boneDir, sm.transform.right);

            joint.connectedBody = bodies[s.parent];
            joint.anchor = Vector3.zero;
            joint.autoConfigureConnectedAnchor = true;
            joint.axis = t.InverseTransformDirection(hinge.normalized);
            joint.secondaryAxis = t.InverseTransformDirection(boneDir);
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Limited;
            joint.lowAngularXLimit = new SoftJointLimit { limit = -s.flex };
            joint.highAngularXLimit = new SoftJointLimit { limit = s.ext };
            joint.angularYLimit = new SoftJointLimit { limit = s.twist };
            joint.angularZLimit = new SoftJointLimit { limit = s.swing };
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.slerpDrive = new JointDrive { positionSpring = 0f, positionDamper = 0f, maximumForce = float.MaxValue };
            joint.enableCollision = false;
            joint.enablePreprocessing = false;
            joint.projectionMode = JointProjectionMode.None;
            log.AppendLine($"  joint {s.bone} -> {s.parent}  bend -{s.flex}/+{s.ext}  twist {s.twist}  swing {s.swing}");
        }

        // Point PlayerStateMachine's ragdoll arrays at the new set (forearms included).
        var list = new List<Rigidbody>();
        var joints = new List<ConfigurableJoint>();
        var cols = new List<Collider>();
        foreach (var s in Specs)
        {
            var rb = bodies[s.bone];
            list.Add(rb);
            var j = rb.GetComponent<ConfigurableJoint>();
            if (j != null) joints.Add(j);
            cols.AddRange(rb.GetComponents<Collider>());
        }
        var so = new SerializedObject(sm);
        SetArray(so, "<ragdollRigidBodies>k__BackingField", list);
        SetArray(so, "<ragdollJoints>k__BackingField", joints);
        SetArray(so, "<ragdollColliders>k__BackingField", cols);
        so.ApplyModifiedPropertiesWithoutUndo();

        if (sm.GetComponent<PlayerActiveRagdoll>() == null) sm.gameObject.AddComponent<PlayerActiveRagdoll>();

        Debug.Log(log.ToString());
        return true;
    }

    private static void SetArray<T>(SerializedObject so, string name, List<T> items) where T : Object
    {
        var p = so.FindProperty(name);
        if (p == null)
        {
            Debug.LogError("[ActiveRagdollSetup] Field not found: " + name);
            return;
        }
        p.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    private static int DominantAxis(Vector3 v)
    {
        Vector3 a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        return a.x > a.y && a.x > a.z ? 0 : (a.y > a.z ? 1 : 2);
    }

    private static Transform Find(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform c in parent)
        {
            var r = Find(c, name);
            if (r != null) return r;
        }
        return null;
    }
}
