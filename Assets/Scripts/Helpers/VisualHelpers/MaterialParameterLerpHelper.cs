using System.Collections;
using UnityEngine;

public class MaterialParameterLerpHelper : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float appearLerpDuration = 1f;
    [SerializeField] private float fadeOutLerpDuration = 1f;

    [Header("References")]
    [SerializeField] private MeshRenderer mRenderer;

    [Header("Shader")]
    [SerializeField] private string parameterName = "_LerpAmount";

    private Material mat;
    private Coroutine lerpRoutine;
    private float currentLerpAmount;

    private void Awake()
    {
        mat = mRenderer.material;
        currentLerpAmount = mat.GetFloat(parameterName);
    }
    private void OnEnable()
    {
        mRenderer.enabled = true;
        mat.SetFloat(parameterName, 0f);
    }
    private void OnDisable()
    {
        mRenderer.enabled = false;
        mat.SetFloat(parameterName, 0f);
    }
    public void StartLerping()
    {
        if (lerpRoutine != null)
            StopCoroutine(lerpRoutine);

        lerpRoutine = StartCoroutine(ChangeLerpRoutine(1f, appearLerpDuration));
    }
    public void ReverseLerping()
    {
        if (lerpRoutine != null)
            StopCoroutine(lerpRoutine);

        lerpRoutine = StartCoroutine(ChangeLerpRoutine(0f, fadeOutLerpDuration));
    }
    private IEnumerator ChangeLerpRoutine(float targetValue, float duration)
    {
        float startValue = currentLerpAmount;

        if (Mathf.Approximately(duration, 0f))
        {
            currentLerpAmount = targetValue;
            mat.SetFloat(parameterName, currentLerpAmount);
            lerpRoutine = null;
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float t = Mathf.Clamp01(elapsedTime / duration);
            currentLerpAmount = Mathf.Lerp(startValue, targetValue, t);

            mat.SetFloat(parameterName, currentLerpAmount);

            yield return null;
        }

        currentLerpAmount = targetValue;
        mat.SetFloat(parameterName, currentLerpAmount);

        lerpRoutine = null;
    }
}