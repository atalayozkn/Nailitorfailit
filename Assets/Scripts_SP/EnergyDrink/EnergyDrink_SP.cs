using PlayerScripts;
using UnityEngine;

public class EnergyDrink_SP : MonoBehaviour
{
    public void Interact()
    {
        PlayerMove player = FindAnyObjectByType<PlayerMove>();

        if (player != null)
        {
            player.RefillEnergy();
            Destroy(gameObject);
        }
    }
}
