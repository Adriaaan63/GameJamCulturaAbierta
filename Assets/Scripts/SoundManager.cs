using System;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Serializable]
    public struct EntradaSonido
    {
        public string id;
        public AudioClip clip;
    }
    
    [Header("Audio Sources")]
    [SerializeField]private AudioSource musicSource;
    [SerializeField]private AudioSource sfxSource;

    [Header("Audios")] 
    public List<EntradaSonido> SFXs;
    public List<EntradaSonido> Tracks;
    
    private Dictionary<string, AudioClip> soundFXs;
    private Dictionary<string, AudioClip> musicTracks;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        soundFXs = new Dictionary<string, AudioClip>();
        foreach (var effect in SFXs)
        {
            if (!string.IsNullOrEmpty(effect.id) || !soundFXs.ContainsKey(effect.id))
            {
                soundFXs.Add(effect.id, effect.clip);
            }
        }
        
        musicTracks = new Dictionary<string, AudioClip>();
        foreach (var track in Tracks)
        {
            if (!string.IsNullOrEmpty(track.id) || !soundFXs.ContainsKey(track.id))
            {
                musicTracks.Add(track.id, track.clip);
            }
        }
    }

    public void PlaySFX(string clipName, float volMultiplier = 1f)
    {
        if (clipName == null || !soundFXs.ContainsKey(clipName) || sfxSource == null) return;

        sfxSource.PlayOneShot(soundFXs[clipName], Mathf.Clamp01(volMultiplier));
    }

    public void PlayMusic(string trackName, bool loop = true)
    {
        if (musicSource == null || !musicTracks.ContainsKey(trackName)) return;
        AudioClip track = musicTracks[trackName];

        if (musicSource.clip == track && musicSource.isPlaying) return;

        musicSource.clip = track;
        musicSource.loop = loop;
        musicSource.Play();
    }
    
    public void SetMasterVolume(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
    }

    public void SetMusicVolume(float value)
    {
        if (musicSource != null) musicSource.volume = Mathf.Clamp01(value);
    }

    public void SetSFXVolume(float value)
    {
        if (sfxSource != null) sfxSource.volume = Mathf.Clamp01(value);
    }

    
}
