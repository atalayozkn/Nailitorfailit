using UnityEngine;

// Keeps a world-space UI parallel to the gameplay camera so it reads flat and upright from above,
// whichever way its station is turned. Needs no scene reference, so it works inside prefabs.
public class CameraFacingUI : MonoBehaviour
{
    private Transform cam;

    private void LateUpdate()
    {
        if (cam == null)
        {
            var main = Camera.main;
            if (main == null) return;
            cam = main.transform;
        }
        transform.rotation = Quaternion.LookRotation(cam.forward, cam.up);
    }
}
