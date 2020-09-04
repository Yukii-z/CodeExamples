using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class ZoomInPeriodKnot : PeriodKnot
{
    public float lockedTime = 0f;
    private bool _isLocked = false;
    [HideInInspector] public int triggeredTime = 0;
    public float magniTime = 1f;
    private Vector3 knotPos;
    private Vector3 oriVideoScale;
    private Vector3 oriVideoCenter;
    private Coroutine startedCoroutine;

    public void Start()
    {
        knotPos = gameObject.transform.localPosition;
        oriVideoScale = _mp.gameObject.transform.localScale;
        oriVideoCenter = _mp.gameObject.transform.localPosition;
    }

    protected override void _OnKnotPointerIn()
    {
        ZoomIn();
    }

    protected override void _OnKnotPointerOut()
    {
        if(!Services.applicationController.isGamePaused) 
            ZoomOut();
    }
    
    private void ZoomIn()
    {
        if (!_isLocked)
        {
            _isLocked = true;
            Vector3 magniCenter = new Vector3((knotPos.x-oriVideoCenter.x)* magniTime, (knotPos.y-oriVideoCenter.y)* magniTime,oriVideoCenter.z);
            _mp.gameObject.transform.localScale = oriVideoScale * magniTime;
            _mp.gameObject.transform.localPosition = new Vector3(oriVideoCenter.x - magniCenter.x,
                oriVideoCenter.y - magniCenter.y, oriVideoCenter.z);
            
            startedCoroutine = CoroutineManager.DoDelayCertainSeconds(delegate { _isLocked = false; ZoomOut();}, lockedTime);
        }
    }
    
    private void ZoomOut()
    {
        if (!_isLocked && !Services.inputController.isPointerOnGameObject(gameObject))
        {
            _mp.gameObject.transform.localScale = oriVideoScale;
            _mp.gameObject.transform.localPosition = oriVideoCenter;
            triggeredTime++;

            CoroutineManager.DoDelayCertainSeconds(delegate { _isLocked = false; }, lockedTime);
        }
    }

    protected override void _OnDisactive()
    {
        _mp.gameObject.transform.localScale = oriVideoScale;
        _mp.gameObject.transform.localPosition = oriVideoCenter;
        if(!object.Equals(startedCoroutine,null)) StopCoroutine(startedCoroutine);
    }
    
    #region Editor
    public override string discription => "Zoom Hotspot";

    #endregion
}
