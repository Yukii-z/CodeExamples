//Knot Manager is used to track and manage all the knots that are related to
//the current playing video.


using System;
using System.Collections;
using System.Collections.Generic;
using HieracheyUtil;
using RenderHeads.Media.AVProVideo;
using TMPro;
using UnityEngine;

public enum knotAppearType
{
    natual,
    basedOnVideo,
    basedOnTime,
}

public class KnotManager
{
    private List<Knot> _trackingKnot = new List<Knot>();
    private List<Knot> _bufferAddKnotList = new List<Knot>();
    private List<Knot> _bufferDeleteKnotList = new List<Knot>();
    private Video? _currentVideo => Services.videoManager.currentVideo;
    private float _lastFrameVideoTime = 0f;
    
    
    public KnotManager SetVideoKnotsTrack(Video video)
    {
        foreach (var knot in video.knotList)
            knot.SetTrack();
        return this;
    }

    public KnotManager SetVideoKnotsInactive(Video video)
    {
        foreach (var knot in video.knotList)
            knot.SetDisactive();
        return this;
    }

    public KnotManager SetVideoKnotsUnTrack(Video video)
    {
        foreach (var knot in video.knotList)
            knot.SetUnTrack();
        return this;
    }

    #region Lifecycle

        public void Update()
        {
            //if the game pause, the this update should not happen
            if(Services.applicationController.isGamePaused) return;
            
            var videoTime = _currentVideo?.mediaPlayer.Control.GetCurrentTimeMs() / 1000f;
    
            _PrepareTrackingKnot();
            foreach (var knot in _trackingKnot)
            {
                if (knot.GetType().IsSubclassOf(typeof(PeriodKnot)))
                {
                    PeriodKnot typeKnot = knot as PeriodKnot;
                    if (typeKnot.appearType != knotAppearType.basedOnVideo) continue;
                    if ((typeKnot.activePosition < videoTime && typeKnot.activePosition >= _lastFrameVideoTime) ||
                        (typeKnot.disactivePosition > videoTime && typeKnot.disactivePosition <= _lastFrameVideoTime) ||
                        (typeKnot.activePosition == 0f && videoTime < typeKnot.disactivePosition && !typeKnot.isActive) ||
                        (typeKnot.disactivePosition == typeKnot.parentVideo.mediaPlayer.Info.GetDurationMs() && videoTime > typeKnot.activePosition && !typeKnot.isActive))
                    {
                        typeKnot.SetActive();
                    }
    
                    if ((typeKnot.activePosition >= videoTime && typeKnot.activePosition < _lastFrameVideoTime) ||
                        (typeKnot.disactivePosition <= videoTime && typeKnot.disactivePosition > _lastFrameVideoTime))
                    {
                        typeKnot.SetInactive();
                    }
                }
    
                if (knot.GetType().IsSubclassOf(typeof(EventKnot)))
                {
                    EventKnot typeKnot = knot as EventKnot;
                    if (typeKnot.appearType != knotAppearType.basedOnVideo) continue;
                    if (typeKnot.knotActPosition == 0f) Debug.Log(typeKnot + " has the video position set to 0!");
                    if (typeKnot.knotActPosition <= videoTime && typeKnot.knotActPosition > _lastFrameVideoTime)
                    {
                        typeKnot.SetActive();
                    }
                }
    
                knot.SetUpdate();
            }
    
            _PrepareTrackingKnot();
    
            _lastFrameVideoTime = (float) videoTime;
        }
    
        public void Clear()
        {
            foreach (var knot in _trackingKnot)
                knot.SetInactive().SetUnTrack();
            _trackingKnot.Clear();
            _bufferAddKnotList.Clear();
            _bufferDeleteKnotList.Clear();
        }

    #endregion
    
    private void _PrepareTrackingKnot()
    {
        for (int i = 0; i < _bufferAddKnotList.Count; i++)
            _trackingKnot.Add(_bufferAddKnotList[i]);
        for (int i = 0; i < _bufferDeleteKnotList.Count; i++)
        {
            if (!_trackingKnot.Contains(_bufferDeleteKnotList[i])) continue;
            _trackingKnot.Remove(_bufferDeleteKnotList[i]);
        }

        _bufferAddKnotList.Clear();
        _bufferDeleteKnotList.Clear();
    }
    
    public class Knot : MonoBehaviour
    {
        protected MediaPlayer _mp => parentVideo.mediaPlayer;
        protected KnotManager _km => Services.knotManager;
        

        [HideInInspector]
        public string parentVideoName;
        public Video parentVideo => Services.videoManager.GetVideo(parentVideoName);

        [HideInInspector]
        public bool isTracking { get; private set; }
        public bool isActive;

        protected List<Coroutine> _activeRoutine = new List<Coroutine>();
        public List<Coroutine> activeRoutine => _activeRoutine;

        protected List<Coroutine> _trackRoutine = new List<Coroutine>();
        public List<Coroutine> trackRoutine => _trackRoutine;

        public Knot SetUnTrack()
        {
            if (!isTracking) return this;
            if (!_km._trackingKnot.Contains(this)) return this;

            _OnUntrack();
            isTracking = false;
            _km._bufferDeleteKnotList.Remove(this);
            foreach (var coroutine in _trackRoutine)
            {
                if (!object.Equals(coroutine, null)) StopCoroutine(coroutine);
            }
            return this;
        }

        public Knot SetTrack()
        {
            if (isTracking) return this;
            
            _km._bufferAddKnotList.Add(this);
            isTracking = true;
            _OnTrackBegin();
            return this;
        }

        public Knot SetUpdate()
        {
            if (!isTracking || !isActive) return this;
            if (Services.applicationController.isGamePaused) return this;
            _OnUpdate();
            return this;
        }
        
        // has to include isActive = true
        public Knot SetActive()
        {
            if (!isTracking || isActive) return this;

            isActive = true;
            _OnActive();
            Services.eventManager.AddHandler<PointerIn>(OnPointerIn);
            Services.eventManager.AddHandler<PointerOut>(OnPointerOut);
            Services.eventManager.Fire(new PointerIn(Services.inputController.collideObj));
            return this;
        }

        // has to include isActive = false
        public Knot SetInactive()
        {
            if (!isTracking || !isActive) return this;

            foreach (var coroutine in _activeRoutine)
            {
                if (!object.Equals(coroutine, null)) StopCoroutine(coroutine);
            }

            Services.eventManager.RemoveHandler<PointerIn>(OnPointerIn);
            Services.eventManager.RemoveHandler<PointerOut>(OnPointerOut);
            isActive = false;
            _OnInactive();
            return this;
        }
        
        // for event system
        protected void OnPointerIn(PointerIn e)
        {
            if (Services.applicationController.isGamePaused) return;
            if (!isTracking || !isActive) return;
            if (e.collidedObj != gameObject) return;
            _OnKnotPointerIn();
        }

        protected void OnPointerOut(PointerOut e)
        {
            if (Services.applicationController.isGamePaused) return;
            if (!isTracking || !isActive) return;
            if (e.collidedObj != gameObject) return;
            _OnKnotPointerOut();
        }
        
        // These functions should be defined for all subclasses of knot
        ///They shall not be necessary for the sub-subclasses unless it is needed
        protected virtual void _OnTrackBegin()
        {
        }

        protected virtual void _OnUntrack()
        {
        }
        
        // These functions should be defined in the sub-subclasses of knot
        // since that is the place for defining the actual behavior of the knots
        // Both of them should be something that being called once in a while
        // OnDisactive should be called before the knot being set to not active
        // further logic should be defined in Update and other functions
        
        protected virtual void _OnUpdate()
        {
        }

        protected virtual void _OnActive()
        {
        }

        protected virtual void _OnInactive()
        {
        }

        protected virtual void _OnKnotPointerOut()
        {
        }

        protected virtual void _OnKnotPointerIn()
        {
        }

        #region Editor

        public virtual float ableTime { get; private set; }
        public virtual float disableTime { get; private set; }
        public bool isActiveOnEditor { get; private set; }

        public virtual string discription { get; private set; }
        protected GameObject textObj;
        protected LineRenderer _outline;
        protected virtual Color _textColor { get; private set; }

        public virtual void OnActiveInEditor()
        {
            if (isActiveOnEditor && textObj != null) return;

            textObj = new GameObject();
            textObj.name = "FuntionInstruction";
            textObj.transform.position = this.transform.position;
            textObj.tag = "EditorItem";
            SetParent.SteadyScale(textObj, gameObject);
            var text = textObj.AddComponent<TextMeshPro>();
            text.fontSize = 1f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = _textColor;
            text.text = discription;
            isActiveOnEditor = true;
            _PaintOutline(textObj);
        }

        public virtual void OnDisactiveInEditor()
        {
            if (!isActiveOnEditor && textObj == null) return;
            if (textObj != null) DestroyImmediate(textObj);
            if (_outline != null) _RemoveOutline();
            isActive = false;
        }

        private void _PaintOutline(GameObject parent)
        {
            if (!gameObject.GetComponent<MeshCollider>()) return;
            
            Debug.Log("outline painted");
            var mesh = gameObject.GetComponent<BoxCollider>();
            var c = TransformCalculate.PositionForLocalCoordinate(parent, mesh.bounds.center);
            var size = TransformCalculate.ScaleForLocalCoordinate(parent, mesh.bounds.size);
            float rx = size.x / 2f;
            float ry = size.y / 2f;
            Vector3 p0, p1, p2, p3;
            p0 = c + new Vector3(rx, ry, -20);
            p1 = c + new Vector3(rx, -ry, -20);
            p2 = c + new Vector3(-rx, -ry, -20);
            p3 = c + new Vector3(-rx, ry, -20);


            _outline = parent.AddComponent<LineRenderer>();
            _outline.useWorldSpace = false;
            _outline.positionCount = 5;
            _outline.SetPositions(new Vector3[] {p0, p1, p2, p3, p0});
            _outline.startColor = _textColor;
            _outline.endColor = _textColor;
            _outline.material = new Material(Shader.Find("Sprites/Default"));
            _outline.startWidth = 0.05f;
        }

        private void _RemoveOutline()
        {
            DestroyImmediate(_outline);
        }

        #endregion
    }
}