using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InputController
{
    //speed variable
    private float _touchMaxSpeed = 250f;
    private float _touchMinSpeed = 0.0f;
    private float _mouseMaxSpeed = 250f;
    private float _mouseMinSpeed = 0.0f;

    //buffer info
    private int _bufferSize = 20;
    //float should be between 0f-1f;
    private float _bufferRate = 0.9f;
    private List<bool> _activeTouchBuffer = new List<bool>();
    private List<bool> _activeMouseBuffer = new List<bool>();

    //input detail info for touch and mouse
    public Touch? firstTouch
    {
        get
        {
            if (Input.touchCount == 0) return null;
            return Input.GetTouch(0);
        }
    }
    private Touch? _activeTouch
    {
        get
        {
            Touch t = new Touch();
            float speed = 0f;

            if (Input.touches.Length == 0) return null;

            foreach (var touch in Input.touches)
            {
                if (GetSpeed(touch) >= speed)
                {
                    speed = GetSpeed(touch);
                    t = touch;
                }
            }

            if (speed>=_touchMinSpeed && speed<=_touchMaxSpeed)
                return t;
            return null;
        }
    }
    private int _activeMouse
    {
        get { return Input.GetMouseButton(0) && _mouseSpeed <= _mouseMaxSpeed && _mouseSpeed >= _mouseMinSpeed ? 0 : -1; }

    }
    
    //general input variables
    private bool _isVideoSceneInputActive
    {
        get { return _activeTouch != null || _activeMouse != -1; }
    }
    public bool isBufferedInputActive
    {
        get
        {
            int m = 0;
            foreach (var buffer in _activeMouseBuffer) m += buffer ? 1 : 0;

            int t = 0;
            foreach (var buffer in _activeTouchBuffer) t += buffer ? 1 : 0;

            bool mActive = (float) m / (float) _activeMouseBuffer.Count >= _bufferRate;
            bool tActive = (float) t / (float) _activeTouchBuffer.Count >= _bufferRate;
            if (!mActive && !tActive) return false;
            return true;
        }
    }
    public Vector2? inputPosition
    {
        get
        {
            if (_activeTouch != null)
                return Services.game.currentCamera.ScreenToWorldPoint(_activeTouch.Value.position);
            if(_activeMouse != -1)
                return Services.game.currentCamera.ScreenToWorldPoint(Input.mousePosition);
            return null;
        }
    }
    
    //system tracking info
    private float _mouseSpeed = 0f;
    private Vector2 _mousePosLastFrame;
    private GameObject _collidedObjLastFrame = null;
    private GraphicRaycaster _gr;
    private GraphicRaycaster gr
    {
        get
        {
            if (ReferenceEquals(Services.game.currentCanvas, null))
                return null;
            if (ReferenceEquals(_gr, null))
            {
                _gr = Services.game.currentCanvas.GetComponent<GraphicRaycaster>();
                return _gr;
            }
            return _gr;
        }
    }



    #region Life Cycle

    public void Update()
    {
        //input logic
        if (Input.touchSupported)
        {
            Debug.Log("touch");
            //touch logic
            _activeTouchBuffer.Add(_activeTouch != null);
            if (_activeTouchBuffer.Count >= _bufferSize) _activeTouchBuffer.Remove(_activeTouchBuffer[0]);
            
        }
        else
        {
            //mouse logic
            _activeMouseBuffer.Add(_activeMouse == 0);
            //get mouse move speed
            var newPos = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            _mouseSpeed = (newPos - _mousePosLastFrame).magnitude;
            _mousePosLastFrame = newPos;
            if (_activeMouseBuffer.Count >= _bufferSize) _activeMouseBuffer.Remove(_activeMouseBuffer[0]);
        }

        //collide update logic
        var obj = collideObj;
        if (_collidedObjLastFrame != obj && _collidedObjLastFrame != null)
            Services.eventManager.Fire(new PointerOut(_collidedObjLastFrame));
        if (_collidedObjLastFrame != obj && obj != null) Services.eventManager.Fire(new PointerIn(obj));
        _collidedObjLastFrame = obj;

    }

    #endregion

    #region Collide Info

        public GameObject collideObj
        {
            get
            {
                if (Services.game.GetCurrentState()?.GetType() == typeof(Game.VideoScene))
                {
                    if (!_isVideoSceneInputActive) return null;
                    if (Services.applicationController.isGamePaused) return null;
                    if (Services.game.currentCamera == null) return null;
                    //Raycast might be changed if we change it to a 2D work
                    RaycastHit hit;
                    return Physics.Raycast(new Vector3(inputPosition.Value.x, inputPosition.Value.y, -10f),
                        Vector3.forward,
                        out hit)
                        ? hit.collider.gameObject
                        : null;
                }
                
                var eventData = new PointerEventData(EventSystem.current);
                if (Input.touchSupported)
                {
                    if (Input.touchCount == 0) return null;
                    if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
                    {
                        eventData.position = Input.GetTouch(0).position;
                        var results = new List<RaycastResult>();
                        gr.Raycast(eventData, results);
                        if (results.Count != 0)
                            return results[0].gameObject;
                    }
                }
                else
                {
                    if (!Input.GetMouseButton(0)) return null;
                    if (EventSystem.current.IsPointerOverGameObject())
                    {
                        eventData.position = Input.mousePosition;
                        var results = new List<RaycastResult>();
                        gr.Raycast(eventData, results);
                        if (results.Count != 0)
                            return results[0].gameObject;
                    }
                        
                }
                return null;
            }
        }

        public bool isPointerOnGameObject(GameObject obj)
        {
            if (obj == _collidedObjLastFrame) return true;
            return false;
        }

    #endregion

    #region Event

    #endregion

    #region Static Functions

    private float GetSpeed(Touch touch)
    {
        var velocity = touch.deltaPosition / touch.deltaTime;
        var speed = velocity.magnitude;
        return speed;
    }

    #endregion

}
