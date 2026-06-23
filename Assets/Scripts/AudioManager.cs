using Neo.Utility.Extensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance {
        get;
        protected set;
    }

    protected Dictionary<string, AudioClip> loadedlips = new Dictionary<string, AudioClip>();
    //protected List<AudioSource> sources = new List<AudioSource>();    
    //protected Queue<AudioClip> queue = new Queue<AudioClip>();

    [SerializeField]
    protected Transform sourceParent;

    [SerializeField]
    protected AudioMixerGroup musicMixerGroup;

    [SerializeField]
    protected AudioMixerGroup fxMixerGroup;

    public float MusicVolume {
        get {
            bool success = musicMixerGroup.audioMixer.GetFloat("MusicVolume", out float vol);
            System.Diagnostics.Debug.Assert(success);
            return vol;
        }
        protected set {
            float db = LinearPercentToDb(value);
            bool success = musicMixerGroup.audioMixer.SetFloat("MusicVolume", db);
            System.Diagnostics.Debug.Assert(success);
        }
    }

    public float FxVolume {
        get {
            bool success = fxMixerGroup.audioMixer.GetFloat("FXVolume", out float vol);
            System.Diagnostics.Debug.Assert(success);
            return vol;
        }
        protected set {
            float db = LinearPercentToDb(value);
            bool success = fxMixerGroup.audioMixer.SetFloat("FXVolume", db);
            System.Diagnostics.Debug.Assert(success);
        }
    }

    public static float LogarithmicPercentToDb(float percent)
    {
        // convert 0..1 to -80..20 dB
        percent = Mathf.Clamp01(percent);
        return Mathf.Pow(percent, 0.24f) * 80f - 60f;
    }

    public static float LinearPercentToDb(float percent)
    {
        // convert 0..1 to -80..20 dB
        percent = Mathf.Clamp01(percent);
        return (percent * 100f - 80f);
    }

    protected void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    protected AudioClip FindClip( string name )
    {
        if(loadedlips.TryGetValue(name, out AudioClip clip))
        {
            return clip;
        }

        string relativeFilePath = "Audio/" + name;
        var loadedClip = Resources.Load<AudioClip>(relativeFilePath);
        if(!loadedClip)
        {
            return null;
        }

        loadedlips.Add(name, clip);
        return clip;
    }

    protected AudioSource FindSource(string name)
    {
        var go = new GameObject(name);
        go.transform.Reset();
        go.transform.SetParent(sourceParent, true);
        return go.AddComponent<AudioSource>();
    }

    public float PlayMusic(string name)
    {
        return Play(name, 1f, musicMixerGroup);
    }

    public float Play( string name )
    {
        return Play(name, 1f, fxMixerGroup);
    }

    public float Play(AudioClip clip)
    {
        return Play(clip, 1f, fxMixerGroup);
    }

    public float Play(string name, float volume, AudioMixerGroup mixerGroup)
    {
        var clip = FindClip(name);
        if (!clip)
        {
            return 0f;
        }
        return Play(clip, volume, mixerGroup);
    }

    public float Play( AudioClip clip, float volume, AudioMixerGroup mixerGroup )
    {
        if(clip == null)
        {
            return 0f;
        }

        var source = FindSource(clip.name);
        source.clip = clip;
        source.volume = volume;
        source.Play();
        source.outputAudioMixerGroup = mixerGroup;
        StartCoroutine(Cleanup(source));
        return clip.length;
    }

    protected IEnumerator   Cleanup( AudioSource source )
    {
        yield return new WaitForSeconds(source.clip.length);
        Destroy(source.gameObject);
    }
}