using UnityEngine;

/// <summary>
/// Central entry point for game audio. It bootstraps itself on first use so
/// scene objects can play common SFX without each scene needing local wiring.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public enum ClockTickPitch
    {
        Higher,
        Lower
    }

    private const string ResourcePrefabPath = "AudioManager";

    [SerializeField] private AudioClip clockTickClip;
    [SerializeField] private AudioClip leverSwitchClip;
    [SerializeField] private AudioClip geyserBlowClip;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource geyserLoopSource;
    [SerializeField] private Vector2 higherTickPitchRange = new Vector2(1.1f, 1.25f);
    [SerializeField] private Vector2 lowerTickPitchRange = new Vector2(0.75f, 0.9f);
    [SerializeField] private Vector2 leverSwitchPitchRange = new Vector2(0.92f, 1.08f);
    [SerializeField] private float leverSwitchVolume = 0.5f;
    [SerializeField] private float geyserBlowVolume = 0.1f;

    private static AudioManager instance;
    private int activeGeyserBlowRequests;
    private bool warnedMissingClockTickClip;
    private bool warnedMissingLeverSwitchClip;
    private bool warnedMissingGeyserBlowClip;

    public static AudioManager Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            GameObject prefab = Resources.Load<GameObject>(ResourcePrefabPath);
            if (prefab != null)
            {
                GameObject instanceObject = Instantiate(prefab);
                instance = instanceObject.GetComponent<AudioManager>();
                if (instance == null)
                {
                    Destroy(instanceObject);
                }
            }

            if (instance == null)
            {
                instance = new GameObject(nameof(AudioManager)).AddComponent<AudioManager>();
                Debug.LogWarning(
                    $"{nameof(AudioManager)} could not load Resources/{ResourcePrefabPath}.prefab, " +
                    "so the fallback audio manager has no serialized clip assigned.");
            }

            instance.name = nameof(AudioManager);
            DontDestroyOnLoad(instance.gameObject);
            instance.EnsureAudioSource();
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureAudioSource();
    }

    public void PlayClockTick(ClockTickPitch pitch)
    {
        if (clockTickClip == null)
        {
            WarnMissingClockTickClip();
            return;
        }

        PlayOneShot(clockTickClip, GetPitch(pitch));
    }

    public void PlayLeverSwitch()
    {
        if (leverSwitchClip == null)
        {
            WarnMissingClip(nameof(leverSwitchClip), ref warnedMissingLeverSwitchClip);
            return;
        }

        PlayOneShot(leverSwitchClip, GetRandomPitch(leverSwitchPitchRange), leverSwitchVolume);
    }

    public void StartGeyserBlow()
    {
        if (geyserBlowClip == null)
        {
            WarnMissingClip(nameof(geyserBlowClip), ref warnedMissingGeyserBlowClip);
            return;
        }

        activeGeyserBlowRequests++;
        EnsureGeyserLoopSource();

        if (geyserLoopSource.isPlaying)
        {
            return;
        }

        geyserLoopSource.clip = geyserBlowClip;
        geyserLoopSource.volume = geyserBlowVolume;
        geyserLoopSource.loop = true;
        geyserLoopSource.Play();
    }

    public void StopGeyserBlow()
    {
        activeGeyserBlowRequests = Mathf.Max(0, activeGeyserBlowRequests - 1);
        if (activeGeyserBlowRequests > 0 || geyserLoopSource == null)
        {
            return;
        }

        geyserLoopSource.Stop();
    }

    public void PlayOneShot(AudioClip clip, float pitch = 1f, float volume = 1f)
    {
        if (clip == null)
        {
            return;
        }

        EnsureAudioSource();
        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(clip, volume);
        sfxSource.pitch = 1f;
    }

    private float GetPitch(ClockTickPitch pitch)
    {
        Vector2 range = pitch == ClockTickPitch.Higher
            ? higherTickPitchRange
            : lowerTickPitchRange;
        return GetRandomPitch(range);
    }

    private float GetRandomPitch(Vector2 range)
    {
        return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }

    private void EnsureAudioSource()
    {
        if (sfxSource != null)
        {
            return;
        }

        sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.dopplerLevel = 0f;
    }

    private void EnsureGeyserLoopSource()
    {
        if (geyserLoopSource != null)
        {
            return;
        }

        geyserLoopSource = gameObject.AddComponent<AudioSource>();
        geyserLoopSource.playOnAwake = false;
        geyserLoopSource.spatialBlend = 0f;
        geyserLoopSource.dopplerLevel = 0f;
        geyserLoopSource.loop = true;
        geyserLoopSource.volume = geyserBlowVolume;
    }

    private void WarnMissingClockTickClip()
    {
        WarnMissingClip(nameof(clockTickClip), ref warnedMissingClockTickClip);
    }

    private void WarnMissingClip(string clipFieldName, ref bool hasWarned)
    {
        if (hasWarned)
        {
            return;
        }

        hasWarned = true;
        Debug.LogWarning(
            $"{nameof(AudioManager)} is missing {clipFieldName}. " +
            "Assign it on the Resources/AudioManager prefab.");
    }
}
