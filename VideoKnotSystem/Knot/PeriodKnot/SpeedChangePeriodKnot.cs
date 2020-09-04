using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class SpeedChangePeriodKnot : PeriodKnot
{
    [FormerlySerializedAs("slomoSpeed")] [SerializeField] [Range(0.0f, 5.0f)] private float _targetSpeed = 1.0f;
    [SerializeField] [Range(0.0f,5.0f)] private float _speedTransTime = 0.1f;
    private VideoplayerManager _vpm = Services.videoplayerManager;
    private float _originalVideoSpeed = 1.0f;
    
    protected override void _OnKnotPointerIn()
    {
        SpeedChange();
    }

    protected override void _OnKnotPointerOut()
    {
        if(!Services.applicationController.isGamePaused) {NormalSpeed();}
    }

    protected override void _OnInactive()
    {
        NormalSpeed();
    }

    private void SpeedChange()
    {
        _originalVideoSpeed = _mp.Control.GetPlaybackRate();
        if (_speedTransTime == 0f) _mp.m_PlaybackRate = _targetSpeed; 
        else _vpm.VideoSpeedSmoothChange(_mp,_targetSpeed,_speedTransTime);
    }

    private void NormalSpeed()
    {
        if (_speedTransTime == 0f) _mp.m_PlaybackRate = _originalVideoSpeed;
        else
        {
            _vpm.VideoSpeedSmoothChange(_mp,_originalVideoSpeed,_speedTransTime);
        }
    }
    
    #region Editor
    public override string discription => "Speed Change Spot";
    
    #endregion
}
