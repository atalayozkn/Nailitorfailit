using System.Collections;
using UnityEngine;

public class PuddleHelper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MaterialParameterLerpHelper lerpHelper;
    [SerializeField] private Collider triggerCollider;

    [Header("Duration Settings")]
    [SerializeField] private float puddleAppearDuration;
    [SerializeField] private float puddleRemainDuration;
    [SerializeField] private float puddleDisappearDuration;
    private void OnEnable()
    {
        StopAllCoroutines();
        triggerCollider.enabled = false;
    }
    private void OnDisable()
    {
        StopAllCoroutines();
        triggerCollider.enabled = false;
    }
    public void StartPuddleProcess()
    {
        StartCoroutine(PuddleLifeCycleRoutine());
    }
    private IEnumerator PuddleLifeCycleRoutine()
    {
        yield return StartCoroutine(PuddleAppearRoutine());
        yield return StartCoroutine(PuddleRemainRoutine());
        yield return StartCoroutine(PuddleDisappearRoutine());
    }
    private IEnumerator PuddleAppearRoutine()
    {
        lerpHelper.StartLerping();
        yield return new WaitForSeconds(puddleAppearDuration);
    }
    private IEnumerator PuddleRemainRoutine()
    {
        triggerCollider.enabled = true;
        yield return new WaitForSeconds(puddleRemainDuration);
    }
    private IEnumerator PuddleDisappearRoutine()
    {
        lerpHelper.ReverseLerping();
        yield return new WaitForSeconds(puddleDisappearDuration);
        triggerCollider.enabled = false;
    }
}
