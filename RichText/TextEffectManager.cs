using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Experimental.XR;

public struct UndefinedTagInfo
{
    public string tagName;
    public Dictionary<string, string> attribute;
    public string targetedString;
}
public abstract class Tag
{
    public Dictionary<string, string> attribute { get; set; }

    public void Init(Dictionary<string, string> attribute)
    {
        this.attribute = attribute;
    }
}
public interface ITextEffectTag
{
    string DoTextEffect(string operateString);
}
public interface IGameEffectTag
{
    void DoGameEffect();
}

public interface IGameContributeTag
{
    void SetVariables();
}

public class Delete : Tag, ITextEffectTag
{
    public string DoTextEffect(string operateString)
    {
        if (attribute.ContainsKey("match"))
        {
            operateString = operateString.Replace(attribute["match"], "");
            return operateString;
        }
        return "";
    }
}

/// <summary>
/// ProcessingTag(string) is the only public function in this class
/// the example string should be like
/// "<GirlName>she</GirlName> starts <tag1 time=\"1\">to behavior in </tag1>a pretty wired <Delete>way. However, other people are</Delete> apparently not realizing this."
/// define class to function with certain tags
/// tags without definition will output as undefinedTagInfos
/// </summary>
public class TextEffectManager
{
    public static string ProcessingTags(string sentence,out List<UndefinedTagInfo> undefinedTagInfos, out List<Tag> gameRelatedTags )
    {
        undefinedTagInfos = new List<UndefinedTagInfo>();
        gameRelatedTags = new List<Tag>();
        var splitSentenceList = Regex.Split(sentence, @"(?=[[])|(?<=[]])").ToList();
        while (_GetPairTag(splitSentenceList,out KeyValuePair<Tag,string> startTag, out KeyValuePair<Tag,string> endTag, out Dictionary<string,string> attribute))
        { 
            splitSentenceList = _DoTagBehaviorAndCleanTag(startTag, endTag, splitSentenceList, out string operateString, out gameRelatedTags);

            if (startTag.Key == null)
            {
                
                undefinedTagInfos.Add(new UndefinedTagInfo
                             {
                                 tagName = startTag.Value.Split(" "[0],',')[0].Substring(1),
                                 attribute = attribute,
                                 targetedString = operateString,
                             });
            }
            
            //_PrintString(splitSentenceList);
        }
        return ListToString(splitSentenceList);
    }


    private static bool _GetPairTag(List<string> stringList,out KeyValuePair<Tag,string> startTag, out KeyValuePair<Tag,string> endTag, out Dictionary<string,string> attribute)
    {
        attribute = new Dictionary<string, string>();
        var startTagName = "";
        var endTagName = "";
        for (int i = 0; i < stringList.Count; i++)
        {
            if (!_GetActiveTag(stringList[i], out bool isStart, out string tagName, out Dictionary<string,string> tagAttribute)) continue;
            var tagInstance = !ReferenceEquals(Type.GetType(tagName),null)? (Tag) Activator.CreateInstance(Type.GetType(tagName)) : null;
            tagInstance?.Init(tagAttribute);
            if (isStart)
            {
                startTagName = tagName;
                attribute = tagAttribute;
                startTag = new KeyValuePair<Tag, string>(tagInstance,stringList[i]);
                continue;
            }
            //is end
            endTagName = tagName;
            endTag = new KeyValuePair<Tag, string>(tagInstance,stringList[i]);
            Debug.Assert(Equals(startTagName,endTagName),"tag "+ endTag.Value.Substring(1) + "overlay with tag " + startTag.Value);
            return true;
        }

        return false;
    }
    private static bool _GetActiveTag(string testString, out bool isStart, out string tagName, out Dictionary<string,string> attribute)
    {
        isStart = false;
        tagName = "";
        attribute = new Dictionary<string,string>();
        
        if (!testString.StartsWith("[") || !testString.EndsWith("]")) return false;
        
        var tagStr = testString.Substring(1, testString.Length - 2).Trim();
        //var tagElement = tagStr.Split(" "[0],',').ToList();
        var tagElement = tagStr.Split(" "[0]).ToList();
        
        tagName = tagElement[0].Trim();
        tagElement.Remove(tagElement[0]);
        
        string elementString = String.Empty;
        foreach (var element in tagElement)
            elementString += element;
        tagElement = tagStr.Split(',').ToList();
        
        if (tagName.StartsWith("'"))
        {
            tagName = tagName.Substring(1);
            //Type type = Type.GetType(tagName);
            //Debug.Assert(Type.GetType(tagName) != null && Type.GetType(tagName).IsSubclassOf(typeof(Tag)), tagName + " is not a defined tag class");
            //tagInstance = ReferenceEquals(Type.GetType(tagName),null)? (Tag) Activator.CreateInstance(Type.GetType(tagName)) : null;
            isStart = false;
        }
        else
        {
            foreach (var element in tagElement)
            {
                element.Trim();
                var pair = element.Split("="[0]);
                Debug.Assert(pair[1].Trim().StartsWith("\"") && pair[1].Trim().EndsWith("\""),pair[1] + " is not a correct input parameter");
                //delete the "" around parameter
                var parameter = pair[1].Trim().Substring(1, pair[1].Trim().Length - 2).Trim();
                if (attribute.ContainsKey(pair[0].Trim())) attribute[pair[0].Trim()] = parameter;
                else attribute.Add(pair[0].Trim(),parameter);
            }
            //Type type = Type.GetType(tagName);
            //Debug.Assert(Type.GetType(tagName) != null && Type.GetType(tagName).IsSubclassOf(typeof(Tag)), tagName + " is not a defined tag class");
            //tagInstance = !ReferenceEquals(Type.GetType(tagName),null)? (Tag) Activator.CreateInstance(Type.GetType(tagName)) : null;
            //tagInstance?.Init(attribute);
            isStart = true;
        }

        return true;
    }
    
    private static List<string> _DoTagBehaviorAndCleanTag(KeyValuePair<Tag,string> startTag, KeyValuePair<Tag,string> endTag, List<string> operateString, out string operateTarget, out List<Tag> gameRelatedTags)
    {
        var startPos = operateString.IndexOf(startTag.Value);
        var endPos = operateString.IndexOf(endTag.Value);
        gameRelatedTags = new List<Tag>();
        operateTarget = "";
        for (int i = startPos + 1; i < endPos; i++)
        {
            operateTarget += operateString[i];
            operateString[i] = "";
        }

        if (startTag.Key != null)
        {
            if (startTag.Key.GetType().GetInterfaces().Contains(typeof(ITextEffectTag)))
            {
                var textEffectTag = (ITextEffectTag) startTag.Key;
                operateString[startPos + 1] = textEffectTag.DoTextEffect(operateTarget);
            }

            if (startTag.Key.GetType().GetInterfaces().Contains(typeof(IGameEffectTag)))
            {
                var textEffectTag = (IGameEffectTag) startTag.Key;
                textEffectTag.DoGameEffect();
            }

            if (startTag.Key.GetType().GetInterfaces().Contains(typeof(IGameContributeTag)))
            {
                var textEffectTag = (IGameContributeTag) startTag.Key;
                textEffectTag.SetVariables();
                gameRelatedTags.Add(startTag.Key);
            }
        }

        operateString.Remove(startTag.Value);
        operateString.Remove(endTag.Value);
        
        return operateString;
    }
    
    private void _PrintString(IEnumerable b)
    {
        foreach (var sentence in b)
        {
            Debug.Log(sentence);
        }

    }
    public static string ListToString(List<string> list)
    {
        var str = "";
        foreach (var item in list)
            str += item;
        return str;
    }
}
