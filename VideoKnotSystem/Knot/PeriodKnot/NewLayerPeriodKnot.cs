﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewLayerPeriodKnot : PeriodKnot
{
    [SerializeField] private List<GameObject> ableLayer = new List<GameObject>();

    protected override void _OnActive()
    {
        foreach (var obj in ableLayer)
            obj.SetActive(true);
    }

    protected override void _OnInactive()
    {
        foreach (var obj in ableLayer)
            obj.SetActive(true);
    }
    
    #region Editor
    public override string discription => ableLayer + " Set Active";
    public override void OnActiveInEditor()
    {
        base.OnActiveInEditor();
        _OnActive();
    }

    public override void OnDisactiveInEditor()
    {
        base.OnDisactiveInEditor();
        _OnInactive();
    }

    #endregion
}
