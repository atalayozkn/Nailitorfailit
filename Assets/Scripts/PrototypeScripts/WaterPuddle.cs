using System.Collections;
using UnityEngine;

public class WaterPuddle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MeshRenderer mRenderer;
    [SerializeField] private Collider col;
    [SerializeField] private PhysicsMaterial defaultPhysicsMaterial;
    [SerializeField] private PhysicsMaterial slipperyPhysicsMaterial;

    [Header("Settings")]
    [SerializeField] private float woodLerpDuration = 5.0f;
    [SerializeField] private float ceramicLerpDuration = 30f;
    [SerializeField] private string parameterName;

    //Private References
    private float initialParameterValue;
    private Coroutine lifeCycleCoroutine;
    private Material instancedMaterial;

    private void Start()
    {
        instancedMaterial = mRenderer.material;
        initialParameterValue = -0.3f;
    }
    public void ActivatePuddle()
    {
        if (lifeCycleCoroutine != null) StopCoroutine(lifeCycleCoroutine);
        lifeCycleCoroutine = StartCoroutine(LifeCycleRoutine());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        lifeCycleCoroutine = null;
    }

    private IEnumerator LifeCycleRoutine()
    {
        yield return StartCoroutine(AppearRoutine(woodLerpDuration));

        Transform targetTransform = gameObject.transform.parent;

        if (targetTransform.gameObject.TryGetComponent<Wood>(out Wood wood)) //Check if object is Wood Tile.
        {
            wood.OnSoak();
            yield return StartCoroutine(DisappearRoutine(woodLerpDuration));
            yield break;
        }

        yield return StartCoroutine(DisappearRoutine(ceramicLerpDuration));
    }

    private IEnumerator AppearRoutine(float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            float currentValue = Mathf.Lerp(initialParameterValue, 0.5f, t);
            instancedMaterial.SetFloat(parameterName, currentValue);
            yield return null;
        }

        instancedMaterial.SetFloat(parameterName, 0.5f);
        col.material = slipperyPhysicsMaterial;
    }

    private IEnumerator DisappearRoutine(float duration)
    {
        float startValue = instancedMaterial.GetFloat(parameterName);
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            float currentValue = Mathf.Lerp(startValue, -0.3f, t);
            instancedMaterial.SetFloat(parameterName, currentValue);
            yield return null;
        }

        instancedMaterial.SetFloat(parameterName, -0.3f);
        col.material = defaultPhysicsMaterial;
    }
}