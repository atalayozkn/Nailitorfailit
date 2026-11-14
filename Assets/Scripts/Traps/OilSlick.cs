using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// One oil strip on the road. Grows behind the oil truck while it spills, stays for remainDuration,
// then fades out. The player slips when entering it (enter only, no re-slip while standing in it).
public class OilSlick : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DecalProjector decal;
    [SerializeField] private BoxCollider trigger;
    [SerializeField] private LayerMask whatIsPlayer;

    [Header("Shape")]
    [Tooltip("Across the strip. Keep it just under the player's jump distance so it can be jumped.")]
    [SerializeField] private float width = 1.6f;
    [Tooltip("Metres of road one texture repeat covers.")]
    [SerializeField] private float tileLength = 2f;
    [SerializeField] private float triggerHeight = 0.6f;
    [SerializeField] private float projectionDepth = 1.5f;

    [Header("Timing")]
    [SerializeField] private float appearDuration = 0.25f;
    [SerializeField] private float remainDuration = 30f;
    [SerializeField] private float disappearDuration = 1.5f;

    private Vector3 forward;
    private float length;
    private bool slippery;

    public bool IsBusy => gameObject.activeSelf;

    private void OnDisable()
    {
        StopAllCoroutines();
        slippery = false;
        trigger.enabled = false;
    }

    public void Begin(Vector3 groundPoint, Vector3 direction)
    {
        direction.y = 0f;
        forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

        transform.SetPositionAndRotation(groundPoint, Quaternion.LookRotation(forward, Vector3.up));
        length = 0.01f;
        ApplyShape();

        slippery = true;
        trigger.enabled = true;
        decal.fadeFactor = 0f;
        gameObject.SetActive(true);
        StartCoroutine(Fade(0f, 1f, appearDuration));
    }

    // Stretches the strip up to the point the truck has reached.
    public void Extend(Vector3 point)
    {
        float reach = Vector3.Dot(point - transform.position, forward);
        if (reach <= length) return;

        length = reach;
        ApplyShape();
    }

    public float Length => length;

    public void Finish()
    {
        StartCoroutine(LifeRoutine());
    }

    private IEnumerator LifeRoutine()
    {
        yield return new WaitForSeconds(remainDuration);

        slippery = false;
        trigger.enabled = false;
        yield return Fade(decal.fadeFactor, 0f, disappearDuration);
        gameObject.SetActive(false);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            decal.fadeFactor = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        decal.fadeFactor = to;
    }

    private void ApplyShape()
    {
        trigger.center = new Vector3(0f, triggerHeight * 0.5f - 0.1f, length * 0.5f);
        trigger.size = new Vector3(width, triggerHeight, length);

        // Decal child is rotated 90 on X: its Z projects down, its Y runs along the road.
        Transform d = decal.transform;
        d.localPosition = new Vector3(0f, 0.5f, length * 0.5f);
        d.localRotation = Quaternion.Euler(90f, 0f, 0f);
        decal.size = new Vector3(width, length, projectionDepth);
        decal.pivot = new Vector3(0f, 0f, projectionDepth * 0.5f);
        decal.uvScale = new Vector2(1f, Mathf.Max(0.01f, length / tileLength));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!slippery) return;
        if ((whatIsPlayer.value & (1 << other.gameObject.layer)) == 0) return;
        if (!other.gameObject.TryGetComponent(out PlayerStateMachine stateMachine)) return;
        if (stateMachine.isDead) return;

        stateMachine.ChangeToSlippingState();
    }
}
