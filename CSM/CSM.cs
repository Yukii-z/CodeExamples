using System;
using System.Linq;    
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using UnityEngine;
using Object = System.Object;

public abstract class Info
{
    public int id;
    public string name;
    public bool isValid;
    public List<GameObject> relatedObj = new List<GameObject>();
    public Dictionary<Type, Enum> property = new Dictionary<Type, Enum>();
}

//V should be a struct
public class CSM<TContext, V> where TContext : class where V : Info
{
    // States are going to need access to the objects whose state they represent (e.g. PlayerStates
    // need to have access to a player object) The state machine keeps a reference to that context
    // so states can access it.
    // We make the context readonly so we can be sure that states in the machine can't get their
    // context swapped on them.
    private readonly TContext _context;

    private List<Enum> _propertyEnums;

    // We cache the machine's states in a dictionary in case we need to use them again.
    // This bit is entirely optional though...

    private readonly Dictionary<int, V> _stateCache = new Dictionary<int, V>();

    //This is a list of all the state that the CSM contains, while they are like enums and not functioning as FSM
    //The detail of each state's behavior is according to their properties
    public Dictionary<string, V> stateList = new Dictionary<string, V>();

    public void AddState(int id, string name, bool isValid, GameObject[] relatedObj, params Enum[] properties)
    {
        var newState = Activator.CreateInstance<V>();
        newState.id = id;
        newState.name = name;
        newState.isValid = isValid;
        
        foreach (var obj in relatedObj)
        {
            newState.relatedObj.Add(obj);
        }
        
        foreach (var prop in properties)
        {
            Enum defProp;
            if (newState.property.TryGetValue(prop.GetType(), out defProp)) newState.property[prop.GetType()] = prop;
            else{ newState.property.Add(prop.GetType(),prop);}
        }
        
        stateList.Add(newState.name,newState);
    }

    public Dictionary<Enum, StateBehavior> _stateBehavior = new Dictionary<Enum, StateBehavior>();

    /*public void AddBehavior<TState>() where TState : StateBehavior
    {
        //var newState =(TState) Activator.CreateInstance(typeof(TState),enumId);
        var newState = Activator.CreateInstance<TState>();
        Debug.Assert(!object.Equals(newState.enumId,default(Enum)), "One state class doesnt has it's defined enumId'");
        _stateBehavior.Add(newState.enumId, newState);
        newState.Parent = this;
        newState.Init();
    }*/

    // We keep track of the state machine's current state and expose it through a public
    // property in case someone needs to query it.
    public V CurrentState { get; private set; }

    //this has to be set to the first page or otherwise the first previous state will be null;
    public V PreviousState
    {
        get { return _previousState; }
    }

    public V _previousState;

    // We don't want to change the current state in the middle of an update, so when a transition is called
    private V _pendingState;

    // A trivial constructor. We have to initialize the context here since it's readonly.
    public CSM(TContext context)
    {
        _context = context;
    }

    public void Init<TState>(List<Enum> propertyEnums) where TState : StateBehavior
    {
        _propertyEnums = propertyEnums;
        
        List<Type> childList = typeof(TState).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(TState))).ToList();

        foreach (var child in childList)
        { 
            TState a = Activator.CreateInstance(child) as TState;
            a.Parent = this;
            _stateBehavior.Add(a.enumId, a);
        }
        
        // make sure that the number of state behaviors are the same as the number of the enum properties
         V state = Activator.CreateInstance<V>();
        int i = 0;
        foreach (var propEnum in _propertyEnums)
        {
            i += Enum.GetNames(propEnum.GetType()).Length;
        }

        Debug.Assert(!(
                _propertyEnums.Count == state.property.Count &&
                _stateBehavior.Count == i), "initiate CSM " + _context + " failed");
    }

    // We use a simple update method to keep the current state moving along...
    public void Update()
    {
        // Handle any pending transition if someone called TransitionTo externally (although they probably shouldn't)
        PerformPendingTransition();
        // Make sure there's always a current state to update...
        Debug.Assert(!object.Equals(CurrentState, default),
            "Updating FSM with null current state. Did you forget to transition to a starting state?");
        CallingMethod(CurrentState, "Update");
        // Handle any pending transition that might have happened during the update
        PerformPendingTransition();
    }

    // Queues transition to a new state
    public void TransitionTo(V newState)
    {
        // We do the actual transtion
        
        _pendingState = GetOrCreateState(newState);
    }

    public void TransitionToPreviousState()
    {
        if (!object.Equals(PreviousState, default))
        {
            _pendingState = _previousState;
        }
    }

    // Actually transition to any pending state
    private void PerformPendingTransition()
    {
        if (!object.Equals(_pendingState, null))
        {
            if (!object.Equals(CurrentState, null)) CallingMethod(CurrentState, "OnExit");
            if (!object.Equals(CurrentState, null) && CurrentState.isValid) _previousState = CurrentState;
            CurrentState = _pendingState;
            if (object.Equals(_previousState, null))
            {
                _previousState = CurrentState;
            }
            CallingMethod(CurrentState, "OnEnter");
            _pendingState = null;
        }
    }

    // A helper method to help with managing the caching of the state instances
    private V GetOrCreateState(V newState)
    {
        V state;
        if (_stateCache.TryGetValue(newState.id, out state))
        {
            return newState;
        }
        else
        {
            // This activator business is required to create instances of states
            // using only the type
            //var newState = Activator.CreateInstance<TState>();
            //newState.Parent = this;
            //newState.Init();
            _stateCache[newState.id] = newState;
            return newState;
        }
    }
    
    public V GetGameState(string stateName)
    {
        stateList.TryGetValue(stateName, out var state);
        return state;
    }
    
    private void CallingMethod(V state, string methodName, params Object[] objects)
    {
        foreach (var prop in state.property)
        {
            StateBehavior aimState;
            Debug.Assert(_stateBehavior.TryGetValue(prop.Value, out aimState),
                "the initiation of the CSM is not finished, the number of defined state is not the same as the number of the possible situation");
            _stateBehavior.TryGetValue(prop.Value, out aimState);
            Type t = aimState.GetType();
            MethodInfo mInfo = t.GetMethod(methodName);
            if (mInfo != null)
            {
                mInfo.Invoke(aimState, objects);
            }
        }
    }

    public abstract class StateBehavior
    {
        public Enum enumId;

/*
        protected StateBehavior(Enum enumId)
        {
            this.enumId = enumId;
        }
        

        protected StateBehavior(){}*/

        internal CSM<TContext, V> Parent { get; set; }

        protected TContext Context
        {
            get { return Parent._context; }
        }

        // A convenience method for transitioning from inside a state
        protected void TransitionTo(V newState)
        {
            Parent.TransitionTo(newState);
        }

        protected void TransitionToPreviousState()
        {
            Parent.TransitionToPreviousState();
        }

        // This is called once when the state is first created (think of it like Unity's Awake)
        public virtual void Init()
        {
        }

        // This is called whenever the state becomes active (think of it like Unity's Start)
        public virtual void OnEnter()
        {
        }

        // this is called whenever the state becomes inactive
        public virtual void OnExit()
        {
        }

        // This is your standard update method where most of your work should go
        public virtual void Update()
        {
        }

        // called when the state machine is cleared, and where you should clear resources
        public virtual void CleanUp()
        {
        }

    }
}

