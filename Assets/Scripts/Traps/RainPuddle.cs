using UnityEngine;

// One outdoor rain puddle from RainMutatorController's pool. Mud slows the player; slick puddles (road, sidewalk)
// make the player slip and can be electrified by a falling grid pole.
public class RainPuddle : MonoBehaviour
{
    private const float GrowSpeed = 0.35f;
    private const float ActiveSizeRatio = 0.5f;

    private RainMutatorController controller;
    private MeshRenderer meshRenderer;
    private BoxCollider trigger;
    private Material surfaceMaterial;
    private Material electrifiedMaterial;
    private PlayerStateMachine playerInside;
    private float maxSize;
    private float currentSize;
    private bool isDrying;

    public bool IsMud { get; private set; }
    public bool IsElectrified { get; private set; }

    public void Initialize(RainMutatorController controller, Material electrifiedMaterial)
    {
        this.controller = controller;
        this.electrifiedMaterial = electrifiedMaterial;
        meshRenderer = GetComponent<MeshRenderer>();
        trigger = GetComponent<BoxCollider>();
    }
    public void Place(Vector3 position, Vector3 normal, bool isMud, float size, Material material)
    {
        IsMud = isMud;
        IsElectrified = false;
        maxSize = size;
        currentSize = 0f;
        isDrying = false;
        surfaceMaterial = material;
        meshRenderer.sharedMaterial = material;
        gameObject.layer = isMud ? LayerMask.NameToLayer("Ignore Raycast") : LayerMask.NameToLayer("Slippery");

        transform.SetPositionAndRotation(position, Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(90f, Random.Range(0f, 360f), 0f));
        transform.localScale = new Vector3(0f, 0f, 1f);
        trigger.enabled = false;
        gameObject.SetActive(true);
    }
    public void Dry()
    {
        isDrying = true;
        SetElectrified(false);
    }
    public void SetElectrified(bool condition)
    {
        if (IsMud) return;

        IsElectrified = condition;
        meshRenderer.sharedMaterial = condition ? electrifiedMaterial : surfaceMaterial;
    }
    private void Update()
    {
        currentSize = Mathf.MoveTowards(currentSize, isDrying ? 0f : maxSize, GrowSpeed * Time.deltaTime);
        transform.localScale = new Vector3(currentSize, currentSize, 1f);
        trigger.enabled = currentSize >= maxSize * ActiveSizeRatio;

        if (isDrying && currentSize <= 0f) gameObject.SetActive(false);
    }
    private void OnDisable()
    {
        ReleasePlayer();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent<PlayerStateMachine>(out var stateMachine)) return;

        playerInside = stateMachine;

        if (IsMud)
        {
            controller.AddMudContact(stateMachine);
            return;
        }

        if (stateMachine.currentPlayerState != PlayerStates.Slipping) stateMachine.ChangeToSlippingState();
    }
    private void OnTriggerStay(Collider other)
    {
        if (!IsElectrified) return;
        if (!other.TryGetComponent<PlayerStateMachine>(out var stateMachine)) return;

        stateMachine.SetDeathReason(DeathReason.Electricty);
        stateMachine.ChangeToDeadState();
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerStateMachine>(out _)) ReleasePlayer();
    }
    private void ReleasePlayer()
    {
        if (playerInside == null) return;

        if (IsMud) controller.RemoveMudContact(playerInside);
        playerInside = null;
    }
}
