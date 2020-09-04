using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrackingPeriodKnot : PeriodKnot
{

    protected override void _OnKnotPointerIn()
    {
        parentVideo.Play();
    }

    protected override void _OnKnotPointerOut()
    {
        parentVideo.Pause();
    }

    protected override void _OnInactive()
    {
        Services.videoManager.currentVideo?.mediaPlayer.Play();
    }
    
    #region Editor
    public override string discription => "Track Hotspot";

    #endregion
}
