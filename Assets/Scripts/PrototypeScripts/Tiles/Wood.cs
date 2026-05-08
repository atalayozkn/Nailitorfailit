using System;
using UnityEngine;
using UnityEngine.Events;

public class Wood : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MeshRenderer mRenderer;

    private Material instancedMaterial;

    [Header("Health Settings")]
    [SerializeField] private float discardDuration = 0.1f;
    [SerializeField] private float maxHealth = 1f;
    [SerializeField] private float perHitDmg = 0.1f;

    private float currentHealth;

    [Header("Visual Settings")]
    [SerializeField] private string parameterName;
    [SerializeField] private int materialIndex;

    [Header("Events")]
    [SerializeField] private UnityEvent onHitEvent;
    [SerializeField] private UnityEvent onFireStartEvent;
    [SerializeField] private UnityEvent onDestroyEvent;

    private bool isBurning = false;

    private void OnEnable()
    {
        instancedMaterial = mRenderer.materials[materialIndex]; //Cache Instanced Material
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
    private void UpdateVisual()
    {
        float damageValue = 1f - (currentHealth / maxHealth);
        instancedMaterial.SetFloat(parameterName, damageValue);
    }
}