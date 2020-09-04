using System;
using System.Collections;
using System.Collections.Generic;
using HieracheyUtil;
using RenderHeads.Media.AVProVideo;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

public enum knotAppearType
{
    natual,
    basedOnVideo,
    basedOnTime,
}

public enum knotTriggerType
{
    immediateActive,
    mouseTrigger
}

public class KnotManager
{
    public class Knot : MonoBehaviour
    {
        protected MediaPlayer _mp
        {
            get { return parentVideo.mediaPlayer; }
        }

        protected KnotManager _km
        {
            get { return Services.knotManager; }
        }

        [HideInInspector] public string parentVideoName;

        public Video parentVideo
        {
            get { return Services.videoManager.GetVideo(parentVideoName); }
        }

        [HideInInspector] public bool isTracking { get; private set; }
        public bool isActive;

        protected List<Coroutine> _activeRoutine = new List<Coroutine>();
        

        public List<Coroutine> activeRoutine
        {
            get { return _activeRoutine; }
        }
        
        protected List<Coroutine> _trackRoutine = new List<Coroutine>();
        public List<Coroutine> trackRoutine
        {
            get { return _trackRoutine; }
        }

        /// <summary>
        /// These functions should be defined for all subclasses of knot
        /// They shall  not necessary for the sub-subclasses unless it is needed
        /// </summary>
        protected virtual void _OnTrackBegin()
        {
        }

        protected virtual void _OnUntrack()
        {
        }

        public Knot SetUnTrack()
        {
            if (!isTracking) return this;
            if (!_km.trackingKnot.Contains(this)) return this;

            _OnUntrack();
            isTracking = false;
            _km.bufferDeleteKnotList.Remove(this);
            foreach (var coroutine in _trackRoutine)
            {
                if (!object.Equals(coroutine, null)) StopCoroutine(coroutine);
            }
            return this;
        }

        public Knot SetTrack()
        {
            if (isTracking) return this;
            /*//HotSpot register
            if (knot.GetType().IsSubclassOf(typeof(PeriodKnot)))
            {
                PeriodKnot typeKnot = knot as PeriodKnot;
                if (typeKnot.parentVideo == video)
                {
                    trackingKnot.Add(knot);
                    knot.OnRegister();
                }
            }

            //EventKnot register
            if (knot.GetType().IsSubclassOf(typeof(EventKnot)))
            {
                EventKnot typeKnot = knot as EventKnot;
                if (typeKnot.parentVideo == video)
                {
                    trackingKnot.Add(knot);
                    knot.OnRegister();
                }
            }*/
            _km.bufferAddKnotList.Add(this);
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

        protected virtual void _OnUpdate()
        {
        }

        /// <summary>
        /// These functions should be defined in the sub-subclasses of knot
        /// since that is the place for defining the actual behavior of the knots
        /// Both of them should be something that being called once in a while
        /// OnDisactive should be called before the knot being set to not active
        /// further logic should be defined in Update and other functions
        /// </summary>
        protected virtual void _OnActive()
        {
        }

        protected virtual void _OnDisactive()
        {
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
        public Knot SetDisactive()
        {
            if (!isTracking || !isActive) return this;

            foreach (var coroutine in _activeRoutine)
            {
                if (!object.Equals(coroutine, null)) StopCoroutine(coroutine);
            }

            Services.eventManager.RemoveHandler<PointerIn>(OnPointerIn);
            Services.eventManager.RemoveHandler<PointerOut>(OnPointerOut);
            isActive = false;
            _OnDisactive();
            return this;
        }

        /// <summary>
        /// for event system
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnPointerIn(PointerIn e)
        {
            if (Services.applicationController.isGamePaused) return;
            if (!isTracking || !isActive) return;
            if (e.collidedObj != gameObject) return;
            _OnKnotPointerIn();
        }

        protected virtual void OnPointerOut(PointerOut e)
        {
            if (Services.applicationController.isGamePaused) return;
            if (!isTracking || !isActive) return;
            if (e.collidedObj != gameObject) return;
            _OnKnotPointerOut();
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

    public List<Knot> trackingKnot = new List<Knot>();
    public List<Knot> bufferAddKnotList = new List<Knot>();
    public List<Knot> bufferDeleteKnotList = new List<Knot>();

    public KnotManager SetVideoKnotsTrack(Video video)
    {
        foreach (var knot in video.knotList)
            knot.SetTrack();
        return this;
    }

    public KnotManager SetVideoKnotsDisactive(Video video)
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

    private void _PrepareTrackingKnot()
    {
        for (int i = 0; i < bufferAddKnotList.Count; i++)
            trackingKnot.Add(bufferAddKnotList[i]);
        for (int i = 0; i < bufferDeleteKnotList.Count; i++)
        {
            if (!trackingKnot.Contains(bufferDeleteKnotList[i])) continue;
            trackingKnot.Remove(bufferDeleteKnotList[i]);
        }

        bufferAddKnotList.Clear();
        bufferDeleteKnotList.Clear();
    }

    private Video? _currentVideo
    {
        get { return Services.videoManager.currentVideo; }
    }

    private float _lastFrameVideoTime = 0f;

    public void Init()
    {
    }

    public void Update()
    {
        //if the game pause, the this update should not happen
        if(Services.applicationController.isGamePaused) return;
        
        var videoTime = _currentVideo?.mediaPlayer.Control.GetCurrentTimeMs() / 1000f;

        _PrepareTrackingKnot();
        foreach (var knot in trackingKnot)
        {
            //hotspot tracking
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
                    typeKnot.SetDisactive();
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
        foreach (var knot in trackingKnot)
            knot.SetDisactive().SetUnTrack();
        trackingKnot.Clear();
        bufferAddKnotList.Clear();
        bufferDeleteKnotList.Clear();
    }
}