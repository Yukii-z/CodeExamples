using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Video;

/// <summary>
/// The hot spots are used for events that happens in one period of the time of a video
/// They might have more complicated logic
/// </summary>

public abstract class PeriodKnot : KnotManager.Knot
{
    public knotAppearType appearType = knotAppearType.basedOnVideo;
    public float activePosition = 0f;
    public float disactivePosition = 0f;

    protected override void _OnTrackBegin()
    {
        //The coroutine is used to avoid the knots start when the video is actually paused
        _trackRoutine.Add(StartCoroutine(_TrackCoroutine()));
    }
    
    private IEnumerator _TrackCoroutine()
    {
        while(Services.applicationController.isGamePaused) 
            yield return new WaitForSeconds(0.1f);
        switch (appearType)
        {
            case knotAppearType.natual:
                SetActive();
                break;
            case knotAppearType.basedOnTime:
                _activeRoutine.Add(CoroutineManager.DoDelayCertainSeconds(delegate()
                {
                    SetActive();
                }, activePosition));
                _activeRoutine.Add(CoroutineManager.DoDelayCertainSeconds(delegate()
                {
                    SetDisactive();
                }, disactivePosition));
                break;
        }
    }

    #region Editor

    public override float ableTime => activePosition;
    public override float disableTime => disactivePosition;
    protected override Color _textColor => Color.blue;


    #endregion
}
