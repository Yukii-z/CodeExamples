using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaySoundPeriodKnot : PeriodKnot
{
    private AudioManager am = Services.audioManager;
    public string audioName;
    protected override void _OnKnotPointerIn()
    {
        am.PlayEffectAudio(audioName);
    }
    
}
