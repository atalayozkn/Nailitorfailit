using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Style test only. Lives in the test scene so shared prefabs, materials and scripts stay untouched.
// A real rollout would change the prefabs' materials instead, and this scan would not exist.
public class StyleTestRuntime : MonoBehaviour
{
    [SerializeField] private List<Material> sources = new();
    [SerializeField] private List<Material> targets = new();
    [SerializeField] private Shader sourceShader;
    [SerializeField] private Shader ditherShader;
    [SerializeField] private float scanInterval = 0.5f;

    private readonly Dictionary<Material, Material> map = new();
    private readonly List<Material> buffer = new();

    public void SetData(List<Material> s, List<Material> t, Shader surface, Shader dither)
    {
        sources = s;
        targets = t;
        sourceShader = surface;
        ditherShader = dither;
    }

    private void Awake()
    {
        for (int i = 0; i < sources.Count; i++)
            if (sources[i] != null) map[sources[i]] = targets[i];
    }

    private IEnumerator Start()
    {
        RegisterDither();

        // Catches spawned tiles, cars and carriables, plus scripts that restore original materials.
        var wait = new WaitForSeconds(scanInterval);
        while (true)
        {
            Scan();
            yield return wait;
        }
    }

    // CameraDitherHelper hides renderers with unknown shaders, so add this one to its private table.
    private void RegisterDither()
    {
        var field = typeof(CameraDitherHelper).GetField("ditherShaders", BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var helper in FindObjectsByType<CameraDitherHelper>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (field?.GetValue(helper) is Dictionary<string, Shader> table)
                table[sourceShader.name] = ditherShader;
        }
    }

    private void Scan()
    {
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (r is ParticleSystemRenderer) continue;

            r.GetSharedMaterials(buffer);
            bool changed = false;
            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i] != null && map.TryGetValue(buffer[i], out var target))
                {
                    buffer[i] = target;
                    changed = true;
                }
            }
            if (changed) r.SetSharedMaterials(buffer);
        }
    }
}
