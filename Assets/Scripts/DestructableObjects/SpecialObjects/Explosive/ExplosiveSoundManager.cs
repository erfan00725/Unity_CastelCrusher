using UnityEngine;

public class ExplosiveSoundManager : MonoBehaviour
{
    public SO_AudioConfigBase audioConfig;

    private AudioSource _audioSource;

    private void Awake() => _audioSource = GetComponent<AudioSource>();

    public void PlayExplosionSound(float volumeMagnitude = 1f)
    {
        if (_audioSource && audioConfig) audioConfig.Play(_audioSource, volumeMagnitude);
    }
}