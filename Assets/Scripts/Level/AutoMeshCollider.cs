using UnityEngine;

// A solid collider for a prop that was imported without one: a MeshCollider made from the object's own mesh, so the player
// can not walk through it (the Level 4 basketball court: poles, backboards, fence).
[RequireComponent(typeof(MeshFilter))]
public class AutoMeshCollider : MonoBehaviour
{
    private void Awake()
    {
        if (TryGetComponent(out Collider _)) return;

        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
        if (mesh == null) return;

        gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
    }
}
