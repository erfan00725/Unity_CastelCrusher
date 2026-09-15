using System;
using UnityEngine;

public class DOSoundManager : MonoBehaviour
{
    public SO_AudioConfigBase audioConfig;
    public float hitSoundThreshold = 1f;

    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    public void PlayHitSound(float impactForce = 1f)
    {
        if (_audioSource && impactForce >= hitSoundThreshold)
        {
            audioConfig.Play(_audioSource, impactForce);
        }
    }
}
