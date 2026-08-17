using System.Collections;
using UnityEngine;

public class PuddleHelper : MonoBehaviour
{
    private enum PuddleState
    {
        InActive,
        Transition,
        Active
    }

    [Header("References")]
    [SerializeField] private MaterialParameterLerpHelper lerpHelper;
    [SerializeField] private Collider triggerCollider;
    [SerializeField] private PlayerSlipHelper slipHelper;
    [SerializeField] private ElectrocuteHelper electrocuteHelper;

    [Header("Duration Settings")]
    [SerializeField] private float puddleAppearDuration;
    [SerializeField] private float puddleRemainDuration;
    [SerializeField] private float puddleDisappearDuration;

    private PuddleState currentState;
    private void OnEnable()
    {
        StopAllCoroutines();
        triggerCollider.enabled = false;
        currentState = PuddleState.InActive;
    }
    private void OnDisable()
    {
        StopAllCoroutines();
        triggerCollider.enabled = false;
    }

    #region State

    private void SwitchState(PuddleState state)
    {
        switch (state)
        {
            case PuddleState.InActive:
                slipHelper.SetActivity(false);
                electrocuteHelper.SetActivity(false);
                break;
            case PuddleState.Active:
                slipHelper.SetActivity(true);
                break;
            case PuddleState.Transition:
                slipHelper.SetActivity(false);
                electrocuteHelper.SetActivity(false);
                break;
        }
    }

    #endregion

    #region PuddleAppear/Disappear Related
    public void StartPuddleProcess()
    {
        StartCoroutine(PuddleLifeCycleRoutine());
    }
    private IEnumerator PuddleLifeCycleRoutine()
    {
        SwitchState(PuddleState.Transition);
        yield return StartCoroutine(PuddleAppearRoutine());
        SwitchState(PuddleState.Active);
        yield return StartCoroutine(PuddleRemainRoutine());
        yield return StartCoroutine(PuddleDisappearRoutine());
        SwitchState(PuddleState.InActive);
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
    #endregion

    #region Electricity Related
    public void ElectrocutePuddle()
    {
        if (currentState != PuddleState.Active) return;
        electrocuteHelper.SetActivity(true);
    }

    #endregion
}
