using PlayerScripts;
using UnityEngine;

public class EnergyDrink_SP : MonoBehaviour
{
    public void Interact()
    {
        PlayerMove player = FindObjectOfType<PlayerMove>();

        if (player != null)
        {
            player.RefillEnergy();
            Destroy(gameObject);
        }
    }
}
