using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class NextVideoEventKnot : EventKnot
{
    private VideoManager vm = Services.videoManager;
    public string nextVideoName;
    [SerializeField] private bool doWhenTheVideoFinished = true;
    
    // Start is called before the first frame update
    protected override void _OnTrackBegin()
    {
        base._OnTrackBegin();
        if (doWhenTheVideoFinished)
        {
            appearType = knotAppearType.basedOnVideo;
            knotActPosition = (float) parentVideo.mediaPlayer.Info.GetDurationMs()/1000f-0.1f;
        }
    }

    protected override void _OnActive()
    {
        vm.ChangeTo(nextVideoName);
        Services.eventManager.Fire(new VideoChange());
    }

    #region Editor
    public override string discription => "Next Video";
    #endregion
}
