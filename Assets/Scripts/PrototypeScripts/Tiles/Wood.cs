using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Wood : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MeshRenderer mRenderer;
    [SerializeField] private WaterPuddle puddle;

    private Material instancedMaterial;

    [Header("Health Settings")]
    [SerializeField] private float discardDuration = 0.1f;
    [SerializeField] private float maxHealth = 1f;
    [SerializeField] private float perHitDmg = 0.1f;

    private float currentHealth;

    [Header("Visual Settings")]
    [SerializeField] private string burnParameterName;
    [SerializeField] private int burnMaterialIndex;

    [Header("Soak Settings")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Vector3 soakedScale;
    [SerializeField] private float soakDuration = 5f;

    private Coroutine soakRoutineReference;

    [Header("Events")]
    [SerializeField] private UnityEvent onHitEvent;
    [SerializeField] private UnityEvent onFireStartEvent;
    [SerializeField] private UnityEvent onDestroyEvent;

    private bool isBurning = false;

    private void OnEnable()
    {
        instancedMaterial = mRenderer.materials[burnMaterialIndex]; //Cache Instanced Material
        currentHealth = maxHealth; //Set Current Health as Max
        UpdateVisual(); //Update Visuals to ensure the parameter value
    }

    public void OnFireHit()
    {
        if (!isBurning)
        {
            isBurning = true;
            onFireStartEvent?.Invoke();
        }

        if (currentHealth - perHitDmg <= 0f)
        {
            currentHealth = 0f;
            UpdateVisual();
            onDestroyEvent?.Invoke();
            Destroy(gameObject, discardDuration);
            return;
        }

        currentHealth -= perHitDmg;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        UpdateVisual();
        onHitEvent?.Invoke();
    }

    public void OnSoak()
    {
        if (soakRoutineReference != null)
        {
            StopCoroutine(soakRoutineReference);
        }
        soakRoutineReference = StartCoroutine(SoakingRoutine());
    }

    private IEnumerator SoakingRoutine()
    {
        Vector3 startScale = targetTransform.localScale;
        float elapsedTime = 0f;

        while (elapsedTime < soakDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / soakDuration;
            targetTransform.localScale = Vector3.Lerp(startScale, soakedScale, t);
            yield return null;
        }

        targetTransform.localScale = soakedScale;
        soakRoutineReference = null;
    }
    public void TriggerPuddle()
    {
        puddle.ActivatePuddle();
    }
    private void UpdateVisual()
    {
        float damageValue = 1f - (currentHealth / maxHealth);
        instancedMaterial.SetFloat(burnParameterName, damageValue);
    }
}