using UnityEngine;

[CreateAssetMenu(fileName = "AudioConfigBase", menuName = "Scriptable Objects/Audio/SO_AudioConfigBase")]
public class SO_AudioConfigBase : ScriptableObject
{
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 2f)] public float minPitch = 0.95f;
    [Range(0.1f, 2f)] public float maxPitch = 1.05f;
    
    public void Play(AudioSource source, float volumeMagnitude = 1)
    {
        if (clips.Length == 0 || source.isPlaying) return;
        source.clip = clips[Random.Range(0, clips.Length)];
        source.volume = volume * volumeMagnitude;
        source.pitch = Random.Range(minPitch, maxPitch);
        source.Play();
    }
}
