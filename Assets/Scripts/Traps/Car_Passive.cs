using UnityEngine;

public class Car_Passive : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float minMoveSpeed;
    [SerializeField] private float maxMoveSpeed;

    [Header("Lifetime")]
    [SerializeField] private float maxLifeTime;

    private float counter;
    private float selectedMoveSpeed;

    private void OnEnable()
    {
        counter = 0f;
        selectedMoveSpeed = Random.Range(minMoveSpeed, maxMoveSpeed);
    }

    private void Update()
    {
        counter += Time.deltaTime;

        if (counter >= maxLifeTime)
        {
            gameObject.SetActive(false);
            return;
        }

        transform.position += transform.forward * selectedMoveSpeed * Time.deltaTime;
    }
}