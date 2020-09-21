using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NUnit.Framework;
using RenderHeads.Media.AVProVideo;
using TimeUtil;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using Random = System.Random;

public class VideoEditor : EditorWindow
{
    public VideoEditor()
    {
    }

    [MenuItem("Tools/VideoEditor")]
    public static void ShowWindow()
    {
        GetWindow(typeof(VideoEditor));
    }

    private bool _isPlaying = false;
    private int _frameRate = 24;
    private int _speedTime = 1;
    private bool _isPlayedBackward = false;
    private SlowUpdate su = new SlowUpdate(2);
    private int tab;

    private GameObject _targetVideoObj
    {
        get
        {
            GameObject selectedObj = null;
            foreach (var obj in Selection.gameObjects)
                selectedObj = obj.GetComponent<Type_Video>() != null ? obj : selectedObj;
            return
                selectedObj == null && _playerLastFrame != null ? _playerLastFrame.gameObject : selectedObj;
        }
    }
    private MediaPlayer _targetedMediaPlayer
    {
        get { return _targetVideoObj != null ? _targetVideoObj.GetComponent<MediaPlayer>() : null; }
    }

    private GameObject _emptyObj;
    private GameObject _lineGizmo;
    private MonoGizmo _lineMono
    {
        get
        {
            if (_lineGizmo!=null && _lineGizmo.GetComponent<MonoGizmo>())
                return _lineGizmo.GetComponent<MonoGizmo>();
            return null;
        }
    }
    private RenderType _renderType
    {
        get { return _lineMono.renderType; }
        set { _lineMono.renderType = value; }
    }

    private Color _lineColor
    {
        get { return _lineMono.lineColor; }
        set { _lineMono.lineColor = value; }
    }
    
    private bool _isLineHide
    {
        get { return _lineMono.isLineHide; }
        set { _lineMono.isLineHide = value; }
    }

    private float _videoPos;

    private List<KnotManager.Knot> _targetedVideoKnots = new List<KnotManager.Knot>();

    public enum RenderType
    {
        SideLine,
        StraightLine
    }
    #region FontStyle


    #endregion

    #region Life Cycle

    private void OnGUI()
        {
            tab = GUILayout.Toolbar (tab, new string[] {"Video Edit", "Sequence"});
            switch (tab) {
                case 0:
                    GUILayout.Space(10);
                    EditorGUILayout.ObjectField("Targeted Video", _targetVideoObj != null ? _targetVideoObj : _emptyObj,
                        typeof(GameObject), true);
                    if (GUILayout.Button("Reset"))
                    {
                        Selection.objects = new UnityEngine.Object[0];
                        _CleanData(SceneManager.GetActiveScene());
                        _InitEditorGizmo(SceneManager.GetActiveScene());
                        _playerLastFrame = null;
                    }
                    if (!ReferenceEquals(_targetVideoObj,null))
                    {
                        GUILayout.Space(20);
                        EditorGUILayout.LabelField("Video Play");
                        GUILayout.Space(20);

                        GUILayout.Space(10);
    
                        EditorGUILayout.LabelField("Knot Info");
                        GUILayout.Space(10);
    
                        if (GUILayout.Button("Refresh Knot Info")) _targetedVideoKnots = _RefreshKnot(_targetVideoObj);
                        EditorGUILayout.LabelField("Knot Info");
                        foreach (var knot in _targetedVideoKnots)
                            EditorGUILayout.ObjectField(knot.GetType().Name, knot, typeof(KnotManager.Knot), true);
    
                        GUILayout.Space(10);
                        EditorGUILayout.LabelField("Preview");
                        GUILayout.Space(10);
                        GUI.skin.label.alignment = TextAnchor.UpperRight;
                        EditorGUILayout.LabelField((_targetedMediaPlayer.Info.GetDurationMs() / 1000f).ToString("f2"));
                        GUI.skin.label.alignment = TextAnchor.MiddleLeft;
                        _videoPos = EditorGUILayout.Slider(_videoPos, 0f, (float) _targetedMediaPlayer.Info.GetDurationMs() / 1000f);
                    }
    
                    break;
                case 1:
                    _renderType = (RenderType)EditorGUILayout.EnumPopup("Render Type", _renderType);
                    _lineColor = (Color)EditorGUILayout.ColorField("Line Color", _lineColor);
                    _isLineHide = EditorGUILayout.Toggle("Hide Line", _isLineHide);
                    if (GUILayout.Button("Update Line"))
                    {
                        _lineMono.isUpdate = true;
                    }
                        
                    break;
            }
            
        }
    
        private void Update()
        {
            if (_isTargetVideoChanged())
            {
                _CleanData(SceneManager.GetActiveScene());
                _InitEditorGizmo(SceneManager.GetActiveScene());
            }
            _playerLastFrame = _targetedMediaPlayer;
    
            _CheckKnotActivation();
        }
    
        private void OnInspectorUpdate()
        {
            Repaint();
        }
    
        public void OnEnable()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            _CleanData(SceneManager.GetActiveScene());
            _InitEditorGizmo(SceneManager.GetActiveScene());
            if(!ReferenceEquals(_targetVideoObj,null)) _InitTargetVideo();
            EditorSceneManager.activeSceneChangedInEditMode += _OnActiveSceneChanged;
            EditorSceneManager.sceneSaving += _OnSceneSaving;
            EditorSceneManager.sceneClosing += _OnSceneClosing;
            EditorSceneManager.sceneClosed += _OnSceneClosed;
            
        }
        
        private void OnDestroy()
        {
            Selection.objects = new UnityEngine.Object[0];
            _CleanData(SceneManager.GetActiveScene());
            EditorSceneManager.activeSceneChangedInEditMode -= _OnActiveSceneChanged;
            EditorSceneManager.sceneSaving -= _OnSceneSaving;
            EditorSceneManager.sceneClosing -= _OnSceneClosing;
            EditorSceneManager.sceneClosed -= _OnSceneClosed;
        }

    #endregion
    

    private MediaPlayer _playerLastFrame = null;

    private bool _isTargetVideoChanged()
    {
        if (_playerLastFrame != _targetedMediaPlayer)
            return true;
        return false;
    }
    private void _CleanData(Scene cleanScene)
    {
        _InitVideoParameter();
        _DestroyEditorRelatedObj(cleanScene);
        _targetedVideoKnots = _RefreshKnot(_targetVideoObj);

        if (!ReferenceEquals(_playerLastFrame, null))
            _playerLastFrame = null;
    }

    private void _InitTargetVideo()
    {
        if (!ReferenceEquals(_targetVideoObj,null))
        {
            if (_targetedMediaPlayer.VideoOpened) return;
            _targetedMediaPlayer.OpenVideoFromFile(_targetedMediaPlayer.m_VideoLocation,  _targetedMediaPlayer.m_VideoPath,false);
        }
    }

    private void _InitVideoParameter()
    {
        _videoPos = 0f;
        _isPlaying = false;
        _frameRate = 24;
        _isPlayedBackward = false;
        lastFrameTime = EditorApplication.timeSinceStartup;
    }

    private void _InitEditorGizmo(Scene scene)
    {
        if(SceneManager.GetActiveScene()!=scene) return;
        if(!ReferenceEquals(_lineGizmo,null)) DestroyImmediate(_lineGizmo);
        _lineGizmo = new GameObject();
        _lineGizmo.name = "GizmoLine";
        _lineGizmo.tag = "EditorItem";
        _lineGizmo.AddComponent<MonoGizmo>();
        _lineMono.isUpdate = true;
    }
    private double lastFrameTime = 0f;

    private List<KnotManager.Knot> _RefreshKnot(GameObject targetVideoObj)
    {
        foreach (var knot in _targetedVideoKnots) knot.OnDisactiveInEditor();
        if (targetVideoObj == null) return new List<KnotManager.Knot>();
        return targetVideoObj.transform.parent.GetComponentsInChildren<KnotManager.Knot>().ToList();
    }

    private void _CheckKnotActivation()
    {
        if (_targetedMediaPlayer == null) return;
        foreach (var knot in _targetedVideoKnots)
        {
            if (_targetedMediaPlayer.Control.GetCurrentTimeMs() / 1000f > knot.ableTime && _targetedMediaPlayer.Control.GetCurrentTimeMs() / 1000f < knot.disableTime)
                knot.OnActiveInEditor();
            else knot.OnDisactiveInEditor();
        }
    }

    private string _GetVideoName(VideoPlayer vp)
    {
        var obj = vp.gameObject;
        string vg;
        vg = obj.GetComponent<Type_Head>()?.gameObject.name;
        vg = obj.GetComponentInChildren<Type_Head>()?.gameObject.name;
        vg = obj.transform.parent.GetComponent<Type_Head>()?.gameObject.name;
        return vg;
    }

    private void _DestroyEditorRelatedObj(Scene scene)
    {
        if(!scene.isLoaded) return;
        
        var objList = scene.GetRootGameObjects();
        for(int i = 0; i < objList.Length; i++)
        {
            var allObj = objList[i].GetComponentsInChildren<Transform>();
            for (int m = 0; m < allObj.Length; m++)
                if(allObj[m].CompareTag("EditorItem")) DestroyImmediate(allObj[m].gameObject);
        }
    }

    #region Events
    
    private void _OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        _CleanData(oldScene);
        _InitEditorGizmo(newScene);
    }

    private void _OnSceneClosing(Scene closingScene, bool isRemoved)
    {
        _CleanData(closingScene);
    }

    private void _OnSceneClosed(Scene closedScene)
    {
        _CleanData(SceneManager.GetActiveScene());
        _InitEditorGizmo(SceneManager.GetActiveScene());
    }

    private void _OnSceneSaving(Scene scene, string savePath)
    {
        _CleanData(scene);
    }
    #endregion
    
    #region UpdateVideoLinkLine
    public class MonoGizmo : MonoBehaviour
    {
        [HideInInspector] public RenderType renderType = RenderType.SideLine;
        [HideInInspector] public Color lineColor = Color.white;
        [HideInInspector] public bool isUpdate = false;
        [HideInInspector]public Dictionary<string, Video> _videoList = new Dictionary<string, Video>();
        [HideInInspector]public List<Link> _videoLink = new List<Link>();
        [HideInInspector] public bool isLineHide;
        private List<float> noise = new List<float>();

        public struct Link
        {
            public Video startVideo;
            public Video endVideo;

            public Link(Video start, Video end)
            {
                startVideo = start;
                endVideo = end;
            }
        }
        
        public void UpdateVideoSequenceOrderLine()
        {
            _ResetVideoSequence();
            _ResetVideoLinkList();
            _ResetNoise();
        }

        private void _ResetVideoSequence()
        {
            var _videoDic = new Dictionary<string, Video>();

            //Initiate video list
            var objList = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var obj in objList)
            {
                var heads = obj.GetComponentsInChildren<Type_Head>();
                foreach (var head in heads)
                    _videoDic.Add(head.name, new Video(head.gameObject));
            }

            this._videoList = _videoDic;
        }
        private void _ResetVideoLinkList()
        {
            var _videoLink = new List<Link>();
            foreach (var video in _videoList.Values)
            {
                foreach (var knot in video.knotList)
                {
                    if (knot.GetType() == typeof(NextVideoEventKnot))
                    {
                        var evKnot = knot as NextVideoEventKnot;
                        Debug.Assert(_videoList.ContainsKey(evKnot.nextVideoName),
                            evKnot.nextVideoName + " does not exist in the current video sequence.");
                        if(!_videoList.ContainsKey(evKnot.nextVideoName)) continue;
                        var linkVideo = _videoList[evKnot.nextVideoName];
                        _videoLink.Add(new Link(video, linkVideo));
                    }

                    if (knot.GetType() == typeof(NextVideoPeriodKnot))
                    {
                        var evKnot = knot as NextVideoPeriodKnot;
                        Debug.Assert(_videoList.ContainsKey(evKnot.nextVideoName),
                            evKnot.nextVideoName + " does not exist in the current video sequence.");
                        if(!_videoList.ContainsKey(evKnot.nextVideoName)) continue;
                        var linkVideo = _videoList[evKnot.nextVideoName];
                        _videoLink.Add(new Link(video, linkVideo));
                    }
                }
            }

            this._videoLink = _videoLink;
        }
        private void _ResetNoise()
        {
            noise.Clear();
            for (int i = 0; i < _videoLink.Count; i++)
                noise.Add(UnityEngine.Random.Range(0, 50) / 100f);
        }
        public void DrawSequenceLine()
        {
            foreach (var link in _videoLink)
            {
                var dis = _distance(link.startVideo.headObj.transform.position.y,
                    link.endVideo.headObj.transform.position.y);
                var startPos = link.startVideo.headObj.transform.position;
                var endPos = link.endVideo.headObj.transform.position;
                
                Gizmos.color = lineColor;
                
                if(isLineHide) return;
                switch (renderType)
                {
                    case RenderType.SideLine:
                        var noiseFloat = noise[_videoLink.IndexOf(link)];
                        Gizmos.DrawLine(startPos + new Vector3(0f, noiseFloat, 0f), startPos + new Vector3(dis, noiseFloat, 0f));
                        Gizmos.DrawLine(startPos + new Vector3(dis, noiseFloat, 0f), endPos + new Vector3(dis, noiseFloat,0f ));
                        Gizmos.DrawLine(endPos + new Vector3(0f, noiseFloat, 0f), endPos + new Vector3(dis, noiseFloat, 0f)); 
                        break;
                    case RenderType.StraightLine:
                        Gizmos.DrawLine(startPos + new Vector3(0f, 0f, 0f), endPos + new Vector3(0f, 0f, 0f));
                        break;
                }
            }
        }

        public void DrawName()
        {
            GUIStyle style = new GUIStyle ();
            style.fontSize = 15;
            style.alignment = TextAnchor.MiddleCenter;

            var size = 1f;
            foreach (var video in _videoList.Values)
            {
                var pos = video.headObj.transform.position;
                Handles.color = Color.white;
                Handles.Label(pos - new Vector3(0f,1f,0f), video.name);
            }
        }
        private static float _distance(float start_y, float end_y)
        {
            return Mathf.Abs(start_y - end_y);
        }
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    static void OnDrawGizmos(MonoGizmo scr, GizmoType gizmoType)
    {
        if (!scr.isLineHide)
        {
            scr.DrawSequenceLine();
            scr.DrawName();
        }
        if (scr.isUpdate)
        {
            scr.UpdateVideoSequenceOrderLine();
            scr.isUpdate = false;
        }
    }

    #endregion
    
}
