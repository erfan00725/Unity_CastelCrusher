using System;
using UnityEngine;

public class ProjectileSoundManager : MonoBehaviour
{
    public SO_AudioConfigBase audioConfig;

    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    public void PlayHitSound(float impactForce = 1f)
    {
        if (_audioSource)
        {
            audioConfig.Play(_audioSource, impactForce);
        }
    }
}
