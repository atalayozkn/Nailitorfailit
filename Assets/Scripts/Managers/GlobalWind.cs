using UnityEngine;

// Drives the NailIt/Wind Sway shaders. Wind blows along this object's forward axis.
// Scenes without this component have no sway.
[ExecuteAlways]
public class GlobalWind : MonoBehaviour
{
    static readonly int DirectionId = Shader.PropertyToID("_NailWindDirection");
    static readonly int SpeedId = Shader.PropertyToID("_NailWindSpeed");
    static readonly int StrengthId = Shader.PropertyToID("_NailWindStrength");

    [SerializeField, Min(0f)] float speed = 1.2f;
    [SerializeField, Range(0f, 3f)] float strength = 1f;

    void OnEnable() => Apply();

    void OnValidate() => Apply();

    void Update()
    {
        if (transform.hasChanged)
        {
            Apply();
            transform.hasChanged = false;
        }
    }

    // Shader globals outlive the scene, so this stops the next scene inheriting the wind.
    void OnDisable() => Shader.SetGlobalFloat(StrengthId, 0f);

    void Apply()
    {
        Vector3 direction = transform.forward;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

        Shader.SetGlobalVector(DirectionId, direction);
        Shader.SetGlobalFloat(SpeedId, speed);
        Shader.SetGlobalFloat(StrengthId, isActiveAndEnabled ? strength : 0f);
    }
}
