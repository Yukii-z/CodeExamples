
using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using DG.Tweening;
using DialADemon.Page;
using Ink.Runtime;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Experimental.XR;

//using UnityEngine.iOS;

public class PlotManager 
{
    public struct CalendarPlotTimeSpan
    {
        public DateTime startTime;
        public DateTime potentialBreakTime;
    }
    //Calendar contains all the information about the plots which is ready, preparing and finished with certain trigger time
    public Dictionary<Plot, CalendarPlotTimeSpan> Calendar = new Dictionary<Plot, CalendarPlotTimeSpan>();
    public List<Plot> playingPlot = new List<Plot>();
    public bool isSpeedMode = false;
    
    private DateTime _baseTime = DateTime.MinValue;
    //Calendar system
    private Dictionary<string, Plot> _plots = new Dictionary<string, Plot>();
    private string _currentTextAssetLocation;
    
    //other quick reference

    #region Life Cycle

    public void Init()
        {
    
            //Load all plots' info
            _ParsePlotInfo();

            _baseTime = DateTime.Now;
            //TODO: clear notification
            //UnityEngine.iOS.NotificationServices.ClearLocalNotifications();
        }
        
    public void Update()
    {
        //constantly update the plots' state
        var count = Calendar.Count;
        for (int i = 0; i < count; i++)
        {
            var pair = Calendar.ElementAt(i);
            switch (pair.Key.plotState)
            {
                case PlotState.ReadyToPlay:
                    if (pair.Value.startTime < DateTime.Now)
                    {
                        pair.Key.CheckStartQualification();
                        if (pair.Key.plotState == PlotState.ReadyToPlay && pair.Value.startTime < DateTime.Now)
                            pair.Key.ChangePlotState(PlotState.Playing);
                    }
                    break;
                case PlotState.Playing:
                    pair.Key.Update();
                    _CheckAndBreakIfItsBreakTime(pair);
                    break;
            }
        }

        foreach (var plot in _plots.Values)
        {
            if (plot.plotState == PlotState.StandingBy)
                plot.TryAddToCalendar();
        }
    }

    public void Clear()
    {
        //save all the list of plots
        //save all the plot start time
        //TODO
        //add notification
    }

    #endregion

    #region Public Functions

         public void ClearCalendar()
        {
            //including save and clean data
            //save TODO
            //clean
            Calendar.Clear();
        }
         
         public Plot GetPlot(string name)
        {
            if (_plots.ContainsKey(name)) return _plots[name];
            return null;
        }

    #endregion
    
    #region GameTestFunc

    public void Tool_StartPlot(string name)
    {
        ClearCalendar();
        Tool_AddPlotToCalender(name,
            new CalendarPlotTimeSpan {startTime = DateTime.Now, potentialBreakTime = DateTime.MaxValue});
    }

    public void Tool_AddPlotToCalender(string name, CalendarPlotTimeSpan timeSpan)
    {
        GetPlot(name)?.Tool_AddToCalendar(timeSpan);
    }

    #endregion

    #region Save & Load

    /// <summary>
    /// all these happen without plotmanager update
    /// </summary>
    /// <param name="jsonObject"></param>
    /// <returns></returns>
        public JSONObject Save(JSONObject jsonObject)
        {
            var plotJsonObj = new JSONObject();
            var plotList = new JSONArray();
            foreach (var plotPair in Calendar)
            {
                //save each plot
                var plotJson = new JSONObject();
                plotJson.Add("name",plotPair.Key.name);
                var stringStartTime = (SerializeManager.JsonDateTime) plotPair.Value.startTime;
                var stringBreakTime = (SerializeManager.JsonDateTime) plotPair.Value.potentialBreakTime;
                plotJson.Add("startTime",stringStartTime.value.ToString());
                plotJson.Add("breakTime",stringBreakTime.value.ToString());
                plotJson.Add("plotState",plotPair.Key.plotState.ToString());
                if (plotPair.Key is PhoneCall)
                {
                    var p = plotPair.Key as PhoneCall;
                    plotJson.Add("isPutThrough",p.isPutThrough);
                }
                plotList.Add(plotJson);
            }
            
            plotJsonObj.Add("plotList",plotList);
            var stringBaseTime = (SerializeManager.JsonDateTime) _baseTime;
            plotJsonObj.Add("plotBaseTime",stringBaseTime.value.ToString());

            jsonObject.Add("plot",plotJsonObj);
            return jsonObject;
        }

        public void LoadFromFile(JSONNode jsonObject)
        {

            //reload the calendar
            var plotJsonObj = jsonObject["plot"];
            _baseTime = new SerializeManager.JsonDateTime(Convert.ToInt64((string)plotJsonObj["plotBaseTime"]));
            foreach (var jsonPlotInfo in plotJsonObj["plotList"].Values)
            {
                var plot = GetPlot(jsonPlotInfo["name"]);
                if (plot is PhoneCall)
                {
                    var p = plot as PhoneCall;
                    p.isPutThrough = jsonPlotInfo["isPutThrough"];
                }
                if(Enum.TryParse<PlotState>(jsonPlotInfo["plotState"], out PlotState s))
                    plot.AssignPlotState(s);
                var timeSpan = new CalendarPlotTimeSpan
                {
                    startTime = new SerializeManager.JsonDateTime(Convert.ToInt64((string)jsonPlotInfo["startTime"])),
                    potentialBreakTime = new SerializeManager.JsonDateTime(Convert.ToInt64((string)jsonPlotInfo["breakTime"]))
                };
                Calendar.Add(plot, timeSpan);

                //game load functions
                _LoadPlayingPlots();
               
            }
        }

        public void UpdatePlayerOffTime()
        {
             _LoadPlotsThatShouldStart();
        }
        
        private void _LoadPlayingPlots()
        {
            foreach (var plotPair in Calendar)
            {
                if(plotPair.Key.plotState != PlotState.Playing) return;
                
                _CheckAndBreakIfItsBreakTime(plotPair);
                plotPair.Key.ReloadPlayingPlot();
                playingPlot.Add(plotPair.Key); 
            }
        }
        private void _LoadPlotsThatShouldStart()
        {
            var sortCalendar = _SortCalenderByStartTime();
            var pendingPlots = sortCalendar.Where(pair =>
                pair.Value.startTime < DateTime.Now - TimeSpan.FromMinutes(2) &&
                pair.Key.plotState == PlotState.ReadyToPlay);
            
            //leave the newest text plot and either abandon or break the old ones
            var textPairs = pendingPlots.Where(pair => pair.Key is Text).ToList();
            if (textPairs.Count != 0)
            {
                var startText = textPairs[textPairs.Count - 1];
                startText.Key.ChangePlotState(PlotState.Playing);
                _CheckAndBreakIfItsBreakTime(startText);
                textPairs.Remove(startText);
                foreach (var pair in textPairs)
                    pair.Key.ChangePlotState(PlotState.Abandoned);
            }

            //all the phones are set to missed
            //which is break
            //abandon is like they never exist so we use break here
            var phonePairs = pendingPlots.Where(pair => pair.Key is PhoneCall).ToList();
            foreach (var pair in phonePairs)
                pair.Key.ChangePlotState(PlotState.Broke);
            
        }
    

        #endregion

    #region Private Function

        private bool _CheckAndBreakIfItsBreakTime(KeyValuePair<Plot,CalendarPlotTimeSpan> calendarPlot)
        {
            if (calendarPlot.Value.potentialBreakTime < DateTime.Now && calendarPlot.Key.plotState == PlotState.Playing)
            {
                calendarPlot.Key.ChangePlotState(PlotState.Broke);
                return true;
            }
            return false;
        }

        private List<KeyValuePair<Plot,CalendarPlotTimeSpan>> _SortCalenderByStartTime()
        {
            var list = Calendar.ToList();
            for(var i = 0; i < list.Count - 1; i++) {
                for(var j = 0; j < list.Count - 1 - i; j++) {
                    if(list[j].Value.startTime > list[j+1].Value.startTime) {       
                        var temp = list[j+1];
                        list[j+1] = list[j];
                        list[j] = temp;
                    }
                }
            }
            return list;
        }

        private List<KeyValuePair<Plot, CalendarPlotTimeSpan>> _GetPlotWithStateFromCalendar(PlotState state)
        {
            var list = new List<KeyValuePair<Plot, CalendarPlotTimeSpan>>();
            foreach (var pair in Calendar)
                if(pair.Key.plotState == state) list.Add(pair);
            return list;
        }
        
        #endregion
        
    //when the app quit, save all the info that needed
    //add notification and other stuff to keep track the gameflow
    

    public enum PlotState
    {
        StandingBy,
        ReadyToPlay,
        Playing,
        Finished,
        Broke,
        Abandoned
    }

    public enum PlotInitType
    {
        TimeBased,
        PlotBased,
        Mix,
    }

    public abstract class Plot
    {
        public Plot( 
            string name, PlotInitType initType, int priority, TimeSpan timeBasedSendTimeSpan, TimeSpan plotBasedDelayTime,
            TimeSpan waitTimeBeforeBreak, List<string> prePlots)
        {
            this.name = name;
            this.initType = initType;
            this.priority = priority;
            this.timeBasedSendTimeSpan = timeBasedSendTimeSpan;
            this.plotBasedDelayTime = plotBasedDelayTime;
            this.waitTimeBeforeBreak = waitTimeBeforeBreak;
            this.prePlots = prePlots;
        }
        
        public PlotManager parent;

        private PlotState _plotState = PlotState.StandingBy;

        public PlotState plotState
        {
            get { return _plotState; }
        }
        
        //new sys
        public string name { get; private set; }
        public PlotInitType initType { get; private set; }
        public int priority { get; private set; } = 0;
        public TimeSpan timeBasedSendTimeSpan { get; private set; }
        public DateTime timeBasedSendTime
        {
            get { return parent._baseTime.Date + timeBasedSendTimeSpan; }
        }

        public TimeSpan plotBasedDelayTime { get; private set; }
        public TimeSpan waitTimeBeforeBreak { get; private set; }
        public List<string> prePlots;
        
        //event delegate
        public Action onPlotStart =() =>{ };
        public Action onPlotBreak =() =>{ };
        public Action onPlotFinish =() =>{ };

        protected CalendarPlotTimeSpan? _currentCalendarTimeSpan
        {
            get {
                if (parent.Calendar.ContainsKey(this))
                    return parent.Calendar[this];
                return null;
            }
        }

        #region Life Cycle

            public virtual void ReloadPlayingPlot(){}

            //called when reloading
            //for plots that are supposed to happen in between the time that the player is offline
            public virtual void LateStart(){}
            
            //int when the plot is added to calendar
            public virtual void Init(){}
            //start when it is time for the plot to run
            public virtual void Start(){}
            public virtual void Update(){}

            public virtual void OnFinished(){}

            public virtual void OnBreak(){}

            public virtual void OnAbandon(){}

        #endregion
        
        //this is for handling conflict when a plot is ready to start
        public virtual void CheckStartQualification(){}

        #region Public Func

        public void ChangePlotState(PlotState ps,CalendarPlotTimeSpan span = new CalendarPlotTimeSpan())
        {
            switch (ps)
            {
                case PlotState.ReadyToPlay:
                    parent.playingPlot.Remove(this);
                    parent.Calendar.Add(this, span);
                    Init();
                    break;
                case PlotState.Playing:
                    parent.playingPlot.Add(this);
                    if(Services.game.runState == RunState.Load) LateStart();
                    else Start();
                    onPlotStart.Invoke();
                    break;
                case PlotState.Finished: 
                    parent.playingPlot.Remove(this);
                    OnFinished(); 
                    onPlotFinish.Invoke();
                    parent.Calendar[this] = new CalendarPlotTimeSpan()
                    {
                        startTime = parent.Calendar[this].startTime,
                        potentialBreakTime = DateTime.Now
                    };
                    break;
                case PlotState.Broke:
                    parent.playingPlot.Remove(this);
                    OnBreak();
                    onPlotBreak.Invoke();
                    parent.Calendar[this] = new CalendarPlotTimeSpan()
                    {
                        startTime = parent.Calendar[this].startTime,
                        potentialBreakTime = DateTime.Now
                    };
                    break;
                case PlotState.Abandoned:
                    parent.playingPlot.Remove(this);
                    OnAbandon();
                    CoroutineManager.DoOneFrameDelay((() =>
                    {
                        parent.Calendar[this] = new CalendarPlotTimeSpan()
                        {
                            startTime = parent.Calendar[this].startTime,
                            potentialBreakTime = DateTime.Now
                        };
                    }));
                    break;
            }
            _plotState = ps;
        }
        
        public void AssignPlotState(PlotState ps,CalendarPlotTimeSpan span = new CalendarPlotTimeSpan())
        {
            _plotState = ps;
        }

        public void TryAddToCalendar()
        {
            if (_plotState == PlotState.StandingBy)
            {
                if (_IsPlotTimeSpanAbleToSet(out CalendarPlotTimeSpan span))
                {
                    ChangePlotState(PlotState.ReadyToPlay, span);
                }
            }
        }
        
        public void Tool_AddToCalendar(CalendarPlotTimeSpan span)
        {
            parent.Calendar.Add(this, span);
            Init();
            ChangePlotState(PlotState.ReadyToPlay,span);
        }

        public bool CheckAndBreakIfItsBreakTime()
        {
            if (_currentCalendarTimeSpan?.potentialBreakTime < DateTime.Now && plotState == PlotState.Playing)
            {
                ChangePlotState(PlotState.Broke);
                return true;
            }
            return false;
        }

        #endregion
        
        private bool _IsPlotTimeSpanAbleToSet(out CalendarPlotTimeSpan span)
        {
            span = new CalendarPlotTimeSpan();
            switch (initType)
            {
                case PlotInitType.TimeBased:
                    span.startTime = timeBasedSendTime;
                    span.potentialBreakTime = timeBasedSendTime + waitTimeBeforeBreak;
                    return true;
                case PlotInitType.PlotBased:
                    foreach (var plot in prePlots)
                    {
                        if (parent._plots.ContainsKey(plot))
                        {
                            var state = parent._plots[plot].plotState;
                            if (state == PlotState.StandingBy || state == PlotState.ReadyToPlay ||
                                state == PlotState.Playing) 
                                return false;
                            continue;
                        }
                        Debug.Assert(true,"plot name " + plot + " not found");
                    }

                    if (prePlots.Count == 0)
                    {
                        span.startTime = parent._baseTime + plotBasedDelayTime;
                        span.potentialBreakTime = span.startTime + waitTimeBeforeBreak;
                    }
                    else
                    {
                        span.startTime = parent.Calendar[parent._plots[prePlots[0]]].potentialBreakTime +
                                                             plotBasedDelayTime;
                        span.potentialBreakTime = span.startTime + waitTimeBeforeBreak;
                    }
                    return true;
                case PlotInitType.Mix:
                    if (DateTime.Now > timeBasedSendTime) return false;
                    
                    foreach (var plot in prePlots)
                    {
                        if (parent._plots.ContainsKey(plot))
                        {
                            var state = parent._plots[plot].plotState;
                            if (state == PlotState.StandingBy || state == PlotState.ReadyToPlay ||
                                state == PlotState.Playing) 
                                return false;
                            continue;
                        }
                        Debug.Assert(true,"plot name " + plot + " not found");
                    }
                    span.startTime = timeBasedSendTime;
                    span.potentialBreakTime = timeBasedSendTime + waitTimeBeforeBreak;
                    return true;
            }

            return false;
        }
    }

    public class Text : Plot
    {
        public Text(string name, PlotInitType initType, int priority, TimeSpan timeBasedSendTimeSpan,
            TimeSpan plotBasedDelayTime,
            TimeSpan waitTimeBeforeBreak, List<string> prePlots) : base(name, initType, priority, timeBasedSendTimeSpan,
            plotBasedDelayTime,
            waitTimeBeforeBreak, prePlots){}

        private Story _story;
        public Story story
        {
            get
            {
                if (ReferenceEquals(_story, null))
                {
                    var textAsset = Resources.Load<TextAsset>("InkText/" + name);
                    //var ta = Resources.Load<TextAsset>(textAssetLocation);
                    _story = new Story(textAsset.text);
                    return _story;
                }

                return _story;
            }
        }
        private readonly TextManager _tm = Services.textManager;

        public override void ReloadPlayingPlot(){}

        public override void LateStart()
        {
            parent.Calendar.TryGetValue(this, out CalendarPlotTimeSpan timeSpan);
            Debug.Log("????");
            _tm._LoadDialogueForNewPlotWhenAPPisOff(timeSpan.startTime);
        }

        public override void Init(){}

        public override void Start()
        {
            _tm.ContinueOrStartStory();
        }

        public override void Update(){}

        public override void OnBreak()
        {
            if (_tm.currentText == this) _tm.MuteAllKeyboard();
            _tm.WrapAndSavePlotDialogues();
        }

        public override void OnFinished()
        {
            if (_tm.currentText == this) _tm.MuteAllKeyboard();
            _tm.WrapAndSavePlotDialogues();
            
        }

        public override void OnAbandon(){}

        public void OnSendText()
        {
            var span = new CalendarPlotTimeSpan();
            if (!ReferenceEquals(_currentCalendarTimeSpan, null))
            {
                span = _currentCalendarTimeSpan.Value;
                span.potentialBreakTime = DateTime.Now + waitTimeBeforeBreak;
                parent.Calendar[this] = span;
            }
        }
        
        public void Pause()
        {
            _tm.PauseStory();
        }

        public void UnPause()
        {
            _tm.ContinueOrStartStory();
        }

        public override void CheckStartQualification()
        {
            foreach (var playingPlot in parent.playingPlot)
            {
                if (playingPlot is Text)
                {
                    // make sure no two text plots run in the same time
                    if (priority <= playingPlot.priority)
                        playingPlot.ChangePlotState(PlotState.Broke);
                    else
                        ChangePlotState(PlotState.Abandoned);
                    continue;
                }

                if (playingPlot is PhoneCall)
                {
                    if (playingPlot is PlayerCall)
                    {
                        var phone = playingPlot as PlayerCall;
                        if (!phone.isPutThrough) continue;
                    }

                    var span = parent.Calendar[this];
                    span.startTime += TimeSpan.FromMinutes(3f);
                    span.potentialBreakTime += TimeSpan.FromMinutes(3f);
                    parent.Calendar[this] = span;
                }
            }
        }
    }

    public class PhoneCall : Plot
    {
        public PhoneCall(string name, PlotInitType initType, int priority, TimeSpan timeBasedSendTimeSpan,
            TimeSpan plotBasedDelayTime,
            TimeSpan waitTimeBeforeBreak, List<string> prePlots) : base(name, initType, priority, timeBasedSendTimeSpan,
            plotBasedDelayTime,
            waitTimeBeforeBreak, prePlots){}
        protected readonly PhoneManager _pm = Services.phoneManager;
        private AudioClip _callContent;
        public AudioClip callContent
        {
            get
            {
                if (ReferenceEquals(_callContent, null))
                {
                    _callContent = Services.audioManager.GetAudioClip(name,"PhoneCall/");
                    return _callContent;
                }
                return _callContent;
            }
        }
        public bool isPutThrough = false;

        public void OnCallPutThrough()
        {
            isPutThrough = true;
            
            //pause all text plot
            foreach (var playingPlot in parent.playingPlot)
            {
                if (playingPlot is Text)
                {
                    var textPlayingPlot = playingPlot as Text;
                    textPlayingPlot.Pause();
                    onPlotFinish += () => textPlayingPlot.UnPause();
                    onPlotBreak += () => textPlayingPlot.UnPause();
                }
            }
            var span = new CalendarPlotTimeSpan();
            if (!ReferenceEquals(_currentCalendarTimeSpan, null))
            {
                span = _currentCalendarTimeSpan.Value;
                span.potentialBreakTime = DateTime.MaxValue;
                parent.Calendar[this] = span;
            }
            
        }
    }

    public class DemonCall : PhoneCall
    {
        public DemonCall(string name, PlotInitType initType, int priority, TimeSpan timeBasedSendTimeSpan,
            TimeSpan plotBasedDelayTime,
            TimeSpan waitTimeBeforeBreak, List<string> prePlots) : base(name, initType, priority, timeBasedSendTimeSpan,
            plotBasedDelayTime,
            waitTimeBeforeBreak, prePlots){}
        
        public override void Start()
        {
            _pm.DialIn(this);
        }
        
        public override void CheckStartQualification()
        {
            foreach (var playingPlot in parent.playingPlot)
            {
                if (playingPlot is DemonCall)
                {
                    if (priority <= playingPlot.priority)
                    {
                        var span = parent.Calendar[this];
                        span.startTime += TimeSpan.FromMinutes(3f);
                        span.potentialBreakTime += TimeSpan.FromMinutes(3f);
                        parent.Calendar[this] = span;
                    }
                    else
                        ChangePlotState(PlotState.Abandoned);
                }

                if (playingPlot is PlayerCall)
                {
                    var playerCall = playingPlot as PlayerCall;
                    if (playerCall.isPutThrough)
                    {
                        var span = parent.Calendar[this];
                        span.startTime += TimeSpan.FromMinutes(3f);
                        span.potentialBreakTime += TimeSpan.FromMinutes(3f);
                        parent.Calendar[this] = span;
                    }
                }
            }
        }
    }
    
    public class PlayerCall : PhoneCall
    {
        public PlayerCall(string name, PlotInitType initType, int priority, TimeSpan timeBasedSendTimeSpan,
            TimeSpan plotBasedDelayTime,
            TimeSpan waitTimeBeforeBreak, List<string> prePlots) : base(name, initType, priority, timeBasedSendTimeSpan,
            plotBasedDelayTime,
            waitTimeBeforeBreak, prePlots){}
        public TimeSpan playerWaitTime = TimeSpan.FromSeconds(5f);
        
        public override void CheckStartQualification()
        {
            foreach (var playingPlot in parent.playingPlot)
            {
                if (playingPlot is PlayerCall)
                {
                    if (priority <= playingPlot.priority)
                    {
                        playingPlot.ChangePlotState(PlotState.Broke);
                    }
                    else
                        ChangePlotState(PlotState.Abandoned);
                }
            }
        }
        
    }

    public class Ritual : Plot
    {
        public bool isStart = false;
        public bool isSEnd = false;
        public Ritual(string name, PlotInitType initType, int priority, TimeSpan timeBasedSendTimeSpan,
            TimeSpan plotBasedDelayTime,
            TimeSpan waitTimeBeforeBreak, List<string> prePlots) : base(name, initType, priority, timeBasedSendTimeSpan,
            plotBasedDelayTime,
            waitTimeBeforeBreak, prePlots){}

        private GameObject ritualPrefab;

        public void OnRitualStart()
        {
            isStart = true;
            
            //pause all text plot
            foreach (var playingPlot in parent.playingPlot)
            {
                if (playingPlot is Text)
                {
                    var textPlayingPlot = playingPlot as Text;
                    textPlayingPlot.Pause();
                    onPlotFinish += () => textPlayingPlot.UnPause();
                    onPlotBreak += () => textPlayingPlot.UnPause();
                }
            }
            
            //hang the prefab on the page obj
            foreach (var item in Services.pageState.GetGameState("Ritual_Empty").relatedObj)
            {
                if (item.CompareTag("PageObj"))
                {
                    if (ritualPrefab == null)
                        ritualPrefab = GameObject.Instantiate(FileImporter.GetRitual(name), item.transform);

                    break;
                }
            }
        }

        public override void OnBreak()
        {
            ritualPrefab = null;
        }

        public override void OnFinished()
        {
            ritualPrefab = null;
        }
    }

    public class VoiceMail : Plot
    {
        public VoiceMail(string name, PlotInitType initType, int priority, TimeSpan timeBasedSendTimeSpan,
            TimeSpan plotBasedDelayTime,
            TimeSpan waitTimeBeforeBreak, List<string> prePlots) : base(name, initType, priority, timeBasedSendTimeSpan,
            plotBasedDelayTime,
            waitTimeBeforeBreak, prePlots){}
        
        public bool isSend;

        public override void ReloadPlayingPlot()
        {
            if (isSend)
            {
                //TODO upload the voice mail
            }
        }
        
        public override void Start()
        {
            isSend = true;
            //TODO upload the voice mail
            ChangePlotState(PlotState.Finished);
        }
    }

    #region Plot Input

    private void _ParsePlotInfo()
    {
        var plotDataCSV = Resources.Load<TextAsset>("Data/PlotInfo");
        
        _plots.Clear();
        
        var lines = plotDataCSV.text.Split('\n');
        for (var i = 1; i < lines.Length; i++)
        {
            var splitLine = lines[i].Split(',');
            if (splitLine.Length != 10) continue;

            var name = splitLine[0];
            var initType = (PlotInitType)Enum.Parse(typeof(PlotInitType),splitLine[2]);
            var priority = int.Parse(splitLine[3]);
            var timeBasedSendTimeSpan = splitLine[4] == "" ? TimeSpan.Zero : _ParseTimeSpan(splitLine[4]);
            var plotBasedDelayTimeSpan = splitLine[5] == "" ? TimeSpan.Zero : _ParseTimeSpan(splitLine[5]);
            var waitTimeBeforeBreak = splitLine[6] == "" ? TimeSpan.Zero : _ParseTimeSpan(splitLine[6]);
            var anchorPlotName = splitLine[7];
            var prePlotsName = splitLine.Skip(8).ToList();
            prePlotsName.Remove(anchorPlotName);
            prePlotsName.Insert(0,anchorPlotName);
            prePlotsName = prePlotsName.Where(p => p.Trim() != "").ToList();
            
            object[] para = new object[]{name,initType,priority,timeBasedSendTimeSpan,plotBasedDelayTimeSpan,waitTimeBeforeBreak,prePlotsName};
            Assembly assembly = Assembly.GetExecutingAssembly();
            var toAdd = assembly.CreateInstance("PlotManager+" + splitLine[1].Trim(),true,System.Reflection.BindingFlags.Default,null,para,null,null);
            if (!ReferenceEquals(toAdd, null) && toAdd.GetType().IsSubclassOf(typeof(Plot)))
            {
                var plotToAdd = (Plot) toAdd;
                _plots.Add(name,plotToAdd);
                plotToAdd.parent = this;
            }
        }
    }

    private DateTime _ParseDateTime(DateTime contrast, string toParse)
    {
        var toReturn = new DateTime();
        var timeSpan = _ParseTimeSpan(toParse);

        toReturn = contrast.Date + timeSpan;
        return toReturn;
    }

    private TimeSpan _ParseTimeSpan(string toParse)
    {
        var toReturn = TimeSpan.Zero;
        var infoList = toParse.Trim(new char[]{'d', 'h', 'm'}).Split(new char[]{'d', 'h', 'm'},StringSplitOptions.RemoveEmptyEntries);
        toReturn += TimeSpan.FromDays(int.Parse(infoList[0]));
        toReturn += TimeSpan.FromHours(int.Parse(infoList[1]));
        toReturn += TimeSpan.FromMinutes(int.Parse(infoList[2]));
        return toReturn;
    }

    #endregion
}
