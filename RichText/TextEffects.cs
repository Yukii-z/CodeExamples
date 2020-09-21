using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

public class TextFile : Tag,IGameContributeTag
{
    public string fileBubble;
    public string fileContent;
    public void SetVariables()
    { 
        fileBubble = attribute["fileBubbleName"];
        fileContent = attribute["fileContentName"];
    }
}

public class TypingBehavior : Tag
{
    public float length = 1f;
}

public class doubt :TypingBehavior,IGameContributeTag
{
    public void SetVariables()
    {
        length = 2f;
        if(attribute.ContainsKey("length")) 
            length = Convert.ToSingle(attribute["length"]);
    }
}

public class fast : TypingBehavior, IGameContributeTag
{
    public void SetVariables()
    {
        length = 0.5f;
        if(attribute.ContainsKey("length")) 
            length = Convert.ToSingle(attribute["length"]);
    }
}

public class slow : TypingBehavior, IGameContributeTag
{
    public void SetVariables()
    {
        length = 1.5f;
        if(attribute.ContainsKey("length")) 
            length = Convert.ToSingle(attribute["length"]);
    }
}

public class delay : TypingBehavior, IGameContributeTag
{
    //this length means seconds for waiting
    public void SetVariables()
    {
        length = 1f;
        if(attribute.ContainsKey("length")) 
            length = Convert.ToSingle(attribute["length"]);
    }
}
