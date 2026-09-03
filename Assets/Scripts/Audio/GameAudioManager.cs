using UnityEngine;

public class GameAudioManager : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;

    [Header("Clips")]
    public AudioClip bgmClip;
    public AudioClip tickClip;
    public AudioClip winClip;
    public AudioClip loseClip;

    [Header("Volume")]
    [Range(0f, 1f)] public float bgmVolume  = 0.5f;
    [Range(0f, 1f)] public float tickVolume = 0.8f;
    [Range(0f, 1f)] public float winVolume  = 1f;
    [Range(0f, 1f)] public float loseVolume = 0.8f;

    void Awake()
    {
        if (bgmSource == null)
        {
            bgmSource             = gameObject.AddComponent<AudioSource>();
            bgmSource.loop        = true;
            bgmSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            sfxSource             = gameObject.AddComponent<AudioSource>();
            sfxSource.loop        = false;
            sfxSource.playOnAwake = false;
        }
    }

    void Start()
    {
        PlayBGM();
    }

    public void PlayBGM()
    {
        if (bgmClip == null) return;
        bgmSource.clip   = bgmClip;
        bgmSource.volume = bgmVolume;
        bgmSource.loop   = true;
        bgmSource.Play();
    }

    public void StopBGM() => bgmSource.Stop();

    public void PlayTick()
    {
        if (tickClip == null) return;
        sfxSource.PlayOneShot(tickClip, tickVolume);
    }

    public void PlayWin()
    {
        if (winClip == null) return;
        sfxSource.PlayOneShot(winClip, winVolume);
    }

    public void PlayLose()
    {
        if (loseClip == null) return;
        sfxSource.PlayOneShot(loseClip, loseVolume);
    }
}