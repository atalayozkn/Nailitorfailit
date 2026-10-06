using UnityEngine;

public class SoundController : MonoBehaviour
{
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip[] clips;
    public void PlayAudio(int index)
    {
        source.PlayOneShot(clips[index]);
    }
    public void PlayLoopedAudio(int index)
    {
        source.clip = clips[index];
        source.Play();
    }
    public void RandomizePitch()
    {
        float random = Random.Range(0.75f, 1.25f);
        source.pitch = random;
    }
    public void ReversePitch()
    {
        source.pitch = 1.0f;
    }
    public void StopAudio()
    {
        source.Stop();
    }

}
