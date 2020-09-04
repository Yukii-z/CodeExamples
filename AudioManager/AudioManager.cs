/*
 * This is a customized tool that provides the program with more control
 * of their audios. The audio manager has two types of tracks. One is
 * background music track, which is permanent. And the other is audio effect
 * track, which is only been created when the effect is called and will destroy
 * itself after the effect is finished.
 * Other than the tracks that help the system to arrange audios. The audio
 * manager provides easy access to change the volume, stop and resume the audio etc.
 * The audio manager also provides event API related to audios.
 */


using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AudioManager
{
    /*
     * The MonoBehavior class helps with the functions that need the Unity lifecycle.
     * It will be auto-generated when the Manager initializes.
     */
    internal class AudioManagerMonoBehaviour : MonoBehaviour
    {
        private AudioManager _parent;

        void Update()
        {
                 _CleanDeadSources();
                 _ClearAudioThatFinished();
        }

        public void Init(AudioManager am)
        {
            _parent = am;
        }

        private void _CleanDeadSources()
        {
            foreach (var piece in _parent.discardedPieces)
            {
                if(_parent.trackingPieces.Contains(piece))
                   _parent.trackingPieces.Remove(piece);
            }
            
            _parent.discardedPieces.Clear();

        }

        private void _ClearAudioThatFinished()
        {
            foreach (var audioPiece in _parent.trackingPieces)
                if (!audioPiece.audioSource.isPlaying &&
                    audioPiece.audioSource.time == 0f && audioPiece.playedOnce && !audioPiece.isLoop)
                {
                    audioPiece.onAudioFinished.Invoke();
                    audioPiece.Stop();
                }
        }
        
    }
    
    private AudioManagerMonoBehaviour _audioManagerMonoBehaviour;
    public GameObject ea { get; private set; }
    public GameObject ba { get; private set; }
    public List<AudioPiece> trackingPieces = new List<AudioPiece>();
    public List<AudioPiece> discardedPieces = new List<AudioPiece>();
    public List<AudioSource> backgroundAudioTrack = new List<AudioSource>();
    
    public AudioManager()
    {
        trackingPieces = new List<AudioPiece>(); 
        discardedPieces = new List<AudioPiece>();
        backgroundAudioTrack = new List<AudioSource>();
        _Init(); 
    }

    private void _Init()
    {
        //create the main game object
        var am = new GameObject();
        am.name = "AudioManager";
        _audioManagerMonoBehaviour = am.AddComponent<AudioManagerMonoBehaviour>();
        _audioManagerMonoBehaviour.Init(this);
        
        //create separate obj for storing the audio sources
        ea = new GameObject();
        ea.name = "EffectAudioSources";
        ea.transform.parent = am.transform;
        
        ba = new GameObject();
        ba.name = "BackgroundAudioSources";
        ba.transform.parent = am.transform;

        for (int i = 0; i < 3; i++)
            _CreateNewAudioSource("BackgroundTrack" + i,ba,true);
        
        GameObject.DontDestroyOnLoad(am);
        
    }

    
    //public functions contains creating audioPieces and general adjustment of all the audios 
    #region Public Functions

        public AudioPiece CreateAudioPiece(string audioName,string path = "", int trackNumber = -1)
        {
            var clip = Resources.Load<AudioClip>("Audios/"+ path + audioName);
            return CreateAudioPiece(clip, trackNumber);
        }
        
        public AudioPiece CreateAudioPiece(AudioClip clip, int trackNumber = -1, bool isLoop = false)
        {
            return new AudioPiece(clip, trackNumber, isLoop);
        }
    
        public AudioPiece Play(AudioClip clip, int trackNumber = -1, bool isLoop = false)
        {
            var vp = CreateAudioPiece(clip, trackNumber,isLoop);
            vp.Play();
            return vp;
        }
    
        public AudioClip GetAudioClip(string audioName, string path = "")
        {
            return Resources.Load<AudioClip>("Audios/"+ path + audioName);
        }
    
        public void ChangeSoundEffectVolume(float toVolume)
        {
            foreach (var ap in trackingPieces.Where(o => o.trackNumber == -1))
                ap.audioSource.volume = toVolume;
    
        }
    
        public void ChangeBGMVolume(float toVolume)
        {
            foreach (var ap in trackingPieces.Where(o => o.trackNumber != -1))
                ap.audioSource.volume = toVolume;
        }

    #endregion
    
    #region Private Functions

        private AudioSource _CreateNewAudioSource(string name, GameObject parent, bool isLoop)
        {
            var newAudioObj = new GameObject();
            newAudioObj.name = name + "AudioPlayer";
            var audioSource = newAudioObj.AddComponent<AudioSource>();
            newAudioObj.transform.parent = parent.transform;
            audioSource.loop = isLoop;
            
            return audioSource;
        }

    #endregion
    
}

/*
 * The defined class that is used to store all the information
 * about one audio clip and contain functions that is related
 * to the adjustment of this audio clip.
 */
public class AudioPiece
{
    public AudioPiece(AudioClip clip, int trackNumber = -1, bool isLoop = false)
    {
        audioClip = clip;
        this.trackNumber = trackNumber;
        this.isLoop = isLoop;
    }
    
    public AudioClip audioClip { get; private set; }
    public AudioSource audioSource { get; private set; }
    public int trackNumber{ get; private set; }
    public bool playedOnce = false;
    public bool isLoop = false;
    //event API
    public Action onAudioFinished;
    

    private GameObject _sourceParent => trackNumber == -1 ? am.ea : am.ba;
    private AudioManager am = Services.audioManager;
    
    public AudioPiece Play()
    {
        if (ReferenceEquals(audioSource, null))
        {
            if (trackNumber != -1) 
                audioSource = am.backgroundAudioTrack[trackNumber];
            else
            {
                var newAudioObj = new GameObject();
                newAudioObj.name = audioClip.name + "AudioPlayer";
                audioSource = newAudioObj.AddComponent<AudioSource>();
                audioSource.loop = isLoop;
                newAudioObj.transform.parent = _sourceParent.transform;
            }
        }
        
        if(!am.trackingPieces.Contains(this))
            am.trackingPieces.Add(this);
        audioSource.clip = audioClip;
        audioSource.Play();
        playedOnce = true;
        return this;
    }
    
    public AudioPiece Stop()
    {
        if (ReferenceEquals(audioSource, null)) return this;
        audioSource.Stop();
        if(am.trackingPieces.Contains(this))
            am.discardedPieces.Add(this);
        if (trackNumber == -1) GameObject.Destroy(audioSource.gameObject);
        else audioSource.clip = null;
        return this;
    }

    public AudioPiece Pause()
    {
        if (ReferenceEquals(audioSource, null)) return this;
        audioSource.Pause();
        return this;
    }

    public AudioPiece UnPause()
    {
        if (ReferenceEquals(audioSource, null)) return this;
        audioSource.Play();
        return this;
    }

    public AudioPiece ChangeVolume(float toVolume)
    {
        audioSource.volume = toVolume;
        return this;
    }
}
