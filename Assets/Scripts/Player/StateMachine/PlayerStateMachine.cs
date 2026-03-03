using System;
using System.Collections.Generic;

public class PlayerStateMachine
{
    private Dictionary<Type, PlayerState> states = new Dictionary<Type, PlayerState>();
    public PlayerState CurrentState { get; private set; }
    public PlayerState PreviousState { get; private set; }

    public void RegisterState(PlayerState state)
    {
        states[state.GetType()] = state;
    }

    public void SetState<T>() where T : PlayerState
    {
        if (!states.TryGetValue(typeof(T), out var newState))
        {
            UnityEngine.Debug.LogError($"State {typeof(T).Name} not registered!");
            return;
        }

        PreviousState = CurrentState;
        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();
    }

    public void Update()
    {
        CurrentState?.Update();
    }

    public void FixedUpdate()
    {
        CurrentState?.FixedUpdate();
    }

    public bool IsInState<T>() where T : PlayerState
    {
        return CurrentState != null && CurrentState.GetType() == typeof(T);
    }
}
