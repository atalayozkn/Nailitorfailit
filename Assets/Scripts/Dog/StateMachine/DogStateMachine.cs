using ItemScript;
using UnityEngine;
using UnityEngine.AI;
public class DogStateMachine : StateMachine_Dog
{
    public enum DogState
    {
        Idle,
        Sleep,
        Patrol,
        Chase,
        Bark,
        Inspect,
        Affection,
        Aggresive,
        Play,
        Eat,
        Aid
    }

    [field: Header("References")]
    [field: SerializeField] public Animator animator { get; private set; }
    [field: SerializeField] public SatietyController satietyController { get; private set; }
    [field: SerializeField] public FavorController favorController { get; private set; }
    [field: SerializeField] public DogMovementHandler movementHandler { get; private set; }
    [field: SerializeField] public DogEnergyController energyController { get; private set; }
    [field: SerializeField] public PresenceChecker presenceChecker { get; private set; }
    [field: SerializeField] public Transform carryTransform { get; private set; }
    [field: SerializeField] public FoodBowl foodBowl { get; private set; }
    [field: SerializeField] public Collider[] patrolBoundaries { get; private set; }
    [field: SerializeField] public MailManController mailmanController { get; private set; }
    [field: SerializeField] public CarriableObject_SP currentCarriable { get; private set; }
    [field: SerializeField] public GameObject[] possibleDrops { get; private set; }
    [field: SerializeField] public Transform dropTransform { get; private set; }
    [field: SerializeField] public Transform itemDropTransfom { get; private set; }

    [field: Header("Settings")]
    [field: SerializeField] public int perEatConsumption { get; private set; }
    [field: SerializeField] public int perEatGain { get; private set; }
    [field: SerializeField] public int perPetFavorGain { get; private set; }
    [field: SerializeField] public float maxChaseDistance { get; private set; }
    [field: SerializeField] public float chaseBreakDistance { get; private set; }
    [field: SerializeField] public float patrolBreakDistance { get; private set; }
    [field: SerializeField] public float perSleepGain { get; private set; }
    [field: SerializeField] public DogState currentDogState { get; private set; }
    [field: SerializeField] public int patrolCounter { get; private set; }

    [field: Header("Tracked Targets")]
    [field: SerializeField] public Transform moveTarget { get; private set; }
    [field: SerializeField] public Transform patrolTarget { get; private set; }
    [field: SerializeField] public Transform chaseTarget { get; private set; }
    [field: SerializeField] public Transform playTarget { get; private set; }
    private void OnEnable()
    {
        currentDogState = DogState.Play;
        patrolCounter = 0;
        ChangeToIdleState();
    }
    public void ChangeToIdleState()
    {
        if (currentDogState == DogState.Idle) return;
        currentDogState = DogState.Idle;
        SwitchState(new DogIdleState(this));
    }
    public void ChangeToPatrolState()
    {
        if (currentDogState == DogState.Patrol) return;
        currentDogState = DogState.Patrol;
        SwitchState(new DogPatrolState(this));
    }
    public void ChangeToChaseState()
    {
        if (currentDogState == DogState.Chase) return;
        currentDogState = DogState.Chase;
        SwitchState(new DogChaseState(this));
    }
    public void ChangeToBarkState()
    {
        if (currentDogState == DogState.Bark) return;
        currentDogState = DogState.Bark;
        SwitchState(new DogBarkState(this));
    }
    public void ChangeToEatState()
    {
        if (currentDogState == DogState.Eat) return;
        currentDogState = DogState.Eat;
        SwitchState(new DogEatState(this));
    }
    public void ChangeToInspectState()
    {
        if (currentDogState == DogState.Inspect) return;
        currentDogState = DogState.Inspect;
        SwitchState(new DogInpectState(this));
    }
    public void ChangeToAggresiveState()
    {
        if (currentDogState == DogState.Aggresive) return;
        currentDogState = DogState.Aggresive;
        SwitchState(new DogAggresiveState(this));
    }
    public void ChangeToPlayState()
    {
        if (currentDogState == DogState.Play) return;
        currentDogState = DogState.Play;
        SwitchState(new DogPlayState(this));
    }
    public void ChangeToAffectionState()
    {
        if (currentDogState == DogState.Affection) return;
        currentDogState = DogState.Affection;
        SwitchState(new DogAffectionState(this));
    }
    public void ChangeToAidState()
    {
        if (currentDogState == DogState.Aid) return;
        currentDogState = DogState.Aid;
        SwitchState(new DogAidState(this));
    }
    public void ChangeToSleepState()
    {
        if (currentDogState == DogState.Sleep) return;
        currentDogState = DogState.Sleep;
        SwitchState(new DogSleepState(this));
    }
    public void SetMoveTarget(Transform target)
    {
        moveTarget = target;
    }
    public void SetPlayTarget(Transform target)
    {
        playTarget = target;
    }
    public void SetChaseTarget(Transform target)
    {
        chaseTarget = target;
    }
    public void SetCurrentCarriable(CarriableObject_SP obj)
    {
        currentCarriable = obj;
    }
    public void StopChase()
    {
        chaseTarget = null;
        ChangeToPatrolState();
    }
    public void TriggerPatrolCounter(bool condition)
    {
        if (condition) patrolCounter++;
        else patrolCounter = 0;
    }
    public void RandomizePatrolTarget()
    {
        if (patrolBoundaries == null || patrolBoundaries.Length == 0) return;

        Collider selectedBoundary = patrolBoundaries[Random.Range(0, patrolBoundaries.Length)];

        Bounds bounds = selectedBoundary.bounds;

        for (int i = 0; i < 10; i++)
        {
            float randomX = Random.Range(bounds.min.x, bounds.max.x);
            float randomZ = Random.Range(bounds.min.z, bounds.max.z);

            Vector3 randomPoint = new Vector3(
                randomX,
                transform.position.y,
                randomZ
            );

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            {
                patrolTarget.position = hit.position;
                return;
            }
        }
    }
    public bool ShouldContinueChase()
    {
        if (chaseTarget != null || movementHandler.CheckDistanceToBed() <= maxChaseDistance) return true;
        else return false;
    }
    public void SpawnObject()
    {
        // Pick random index
        int randomIndex = Random.Range(0, possibleDrops.Length);

        // Select prefab
        GameObject selectedObj = possibleDrops[randomIndex];

        // Instantiate under carryTransform
        GameObject obj = Instantiate(selectedObj,dropTransform.position,dropTransform.rotation);

        if (obj.TryGetComponent<CarriableObject_SP>(out CarriableObject_SP carriable))
        {
            currentCarriable = carriable;
            carriable.PickUpByDog(carryTransform);
        }
        else
        {
            currentCarriable = null;
            ChangeToIdleState();
        }
    }

}
