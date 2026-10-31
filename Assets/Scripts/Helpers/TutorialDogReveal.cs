using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

// End of the tutorial: once the dog house is built the dog moves in, its info pop-up explains it,
// then the regular tutorial ending runs. Hook Reveal() to TutorialManager's onTutorialEnd.
public class TutorialDogReveal : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject dogPackage;
    [SerializeField] private Transform dogSpawnPoint;
    [SerializeField] private InfoPopup popup;
    [SerializeField] private InfoPopupData dogInfo;

    [Header("Settings")]
    [Tooltip("Lets the player see the dog arrive before the pop-up opens.")]
    [SerializeField] private float popupDelay = 2f;

    [Header("Events")]
    [SerializeField] private UnityEvent onFinished;

    public void Reveal()
    {
        StartCoroutine(RevealRoutine());
    }

    private IEnumerator RevealRoutine()
    {
        if (dogPackage != null)
        {
            dogPackage.SetActive(true);
            var dog = dogPackage.GetComponentInChildren<DogStateMachine>(true);
            if (dog != null && dogSpawnPoint != null)
            {
                var agent = dog.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled) agent.Warp(dogSpawnPoint.position);
                else dog.transform.position = dogSpawnPoint.position;
                dog.transform.rotation = dogSpawnPoint.rotation;
            }
        }

        yield return new WaitForSecondsRealtime(popupDelay);

        if (popup != null && dogInfo != null) popup.Show(dogInfo, Finish);
        else Finish();
    }

    private void Finish()
    {
        onFinished?.Invoke();
    }
}
