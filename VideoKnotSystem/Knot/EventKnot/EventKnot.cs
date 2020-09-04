using System.Collections;
using System.Collections.Generic;
using RenderHeads.Media.AVProVideo;
using UnityEngine;

// the event knot only get triggered in a specific situation and runs OnActive
// nothing should usually be written in OnInactive func
public class EventKnot : KnotManager.Knot
{

    public knotAppearType appearType = knotAppearType.basedOnVideo;
    public float knotActPosition = 0f;

    protected override void _OnTrackBegin()
    {
        //The coroutine is used to avoid the knots start when the video is actually paused
        _trackRoutine.Add(StartCoroutine(_TrackCoroutine()));
    }

    private IEnumerator _TrackCoroutine()
    {
        while (Services.applicationController.isGamePaused) 
            yield return new WaitForSeconds(0.1f);
        switch (appearType)
        {
            case knotAppearType.natual:
                SetActive();
                break;
            case knotAppearType.basedOnTime:
                _activeRoutine.Add(
                    CoroutineManager.DoDelayCertainSeconds(delegate() { SetActive(); }, knotActPosition));
                break;
        }
    }

    #region Editor

    public override float ableTime => knotActPosition;
    public override float disableTime
    {
        get
        {
            if (gameObject.GetComponent<MediaPlayer>()) return (float)gameObject.GetComponent<MediaPlayer>().Info.GetDurationMs();
            if (gameObject.GetComponentInChildren<MediaPlayer>()) return (float)gameObject.GetComponentInChildren<MediaPlayer>().Info.GetDurationMs();
            if (transform.parent.gameObject.GetComponentInChildren<MediaPlayer>()) return  (float)transform.parent.gameObject.GetComponentInChildren<MediaPlayer>().Info.GetDurationMs();
            Debug.Log("Can not fine the video player for this knot" + this);
            return 0f;
        }
    }

    protected override Color _textColor => Color.red;

    #endregion
}
