using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NextVideoPeriodKnot : PeriodKnot
{
    private VideoManager vm = Services.videoManager;
    public string nextVideoName;
    protected override void _OnKnotPointerIn()
    {
        Services.eventManager.Fire(new VideoChange());
        vm.ChangeTo(nextVideoName);
    }

    protected override void _OnActive()
    {
        Services.visualEffectManager.unfocusedCam.AddTrackingObj(gameObject);
    }

    protected override void _OnInactive()
    {
        Services.visualEffectManager.unfocusedCam.RemoveTrackingObj(gameObject);
    }
}
