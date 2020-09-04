using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class PauseEventKnot : EventKnot
{
    protected override void _OnActive()
    {
        parentVideo.Pause();
    }
    
    #region Editor

    public override string discription => "VideoPause";
    
    #endregion
}
