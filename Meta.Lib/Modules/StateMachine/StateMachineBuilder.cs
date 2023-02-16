using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.StateMachine
{
    public class StateMachineBuilder<TState, TInput>
        where TInput : struct, IComparable
        where TState : struct, IComparable
    {
        readonly Dictionary<TInput, List<IActionWrapper<TState>>> _inputActions =
            new Dictionary<TInput, List<IActionWrapper<TState>>>();

        readonly Dictionary<TState, List<IActionWrapper<TState>>> _entryStateActions =
            new Dictionary<TState, List<IActionWrapper<TState>>>();

        readonly Dictionary<TState, List<IActionWrapper<TState>>> _exitStateActions =
            new Dictionary<TState, List<IActionWrapper<TState>>>();

        readonly ILogger _logger;

        List<IActionWrapper<TState>> _currentInputActions;
        List<IActionWrapper<TState>> _currentEntryStateActions;
        List<IActionWrapper<TState>> _currentExitStateActions;
        TState? _currentState;
        TState _initialState;

        public StateMachineBuilder(ILogger logger = null)
        {
            _logger = logger;

            TInput[] inputArray = (TInput[])Enum.GetValues(typeof(TInput));

            foreach (var item in inputArray)
            {
                _inputActions[item] = new List<IActionWrapper<TState>>();
            }

            TState[] stateArray = (TState[])Enum.GetValues(typeof(TState));

            foreach (var item in stateArray)
            {
                _entryStateActions[item] = new List<IActionWrapper<TState>>();
                _exitStateActions[item] = new List<IActionWrapper<TState>>();
            }
        }

        public StateMachineBuilder<TState, TInput> WithInitialState(TState state)
        {
            _initialState = state;
            return this;
        }

        public StateMachineBuilder<TState, TInput> On(TInput input)
        {
            _currentState = null;
            _currentEntryStateActions = null;
            _currentExitStateActions = null;
            _currentInputActions = _inputActions[input];
            return this;
        }

        public StateMachineBuilder<TState, TInput> OnEntry(TState state)
        {
            _currentInputActions = null;
            _currentState = state;
            _currentExitStateActions = null;
            _currentEntryStateActions = _entryStateActions[state];
            return this;
        }

        public StateMachineBuilder<TState, TInput> OnExit(TState state)
        {
            _currentInputActions = null;
            _currentState = state;
            _currentEntryStateActions = null;
            _currentExitStateActions = _exitStateActions[state];
            return this;
        }

        public StateMachineBuilder<TState, TInput> Set(TState state)
        {
            if (_currentInputActions == null)
                throw new InvalidOperationException("Use On() before call Set()");

            _currentInputActions.Add(
                new SetStateActionWrapper<TState>(SpecialAction.SetState, state, _currentState));

            return this;
        }

        public StateMachineBuilder<TState, TInput> In(TState state)
        {
            _currentState = state;
            return this;
        }

        public StateMachineBuilder<TState, TInput> EndIn()
        {
            _currentState = null;
            return this;
        }

        public StateMachineBuilder<TState, TInput> Do(Action action)
        {
            if (_currentInputActions == null &&
                _currentEntryStateActions == null &&
                _currentExitStateActions == null)
                throw new InvalidOperationException("Use On() before call Do()");

            var actionWrapper = new ActionWrapper<TState>(action, _currentState);

            _currentInputActions?.Add(actionWrapper);
            _currentExitStateActions?.Add(actionWrapper);
            _currentEntryStateActions?.Add(actionWrapper);

            return this;
        }

        public StateMachineBuilder<TState, TInput> DoAsync(Func<Task> action)
        {
            if (_currentInputActions == null &&
                _currentEntryStateActions == null &&
                _currentExitStateActions == null)
                throw new InvalidOperationException("Use On() before call DoAsync()");

            var actionWrapper = new ActionWrapper<TState>(action, _currentState);

            _currentInputActions?.Add(actionWrapper);
            _currentExitStateActions?.Add(actionWrapper);
            _currentEntryStateActions?.Add(actionWrapper);

            return this;
        }

        public ConcurrentStateMachine<TState, TInput> Build(string name = "Unnamed")
        {
            return new ConcurrentStateMachine<TState, TInput>(
                name, _initialState,
                _inputActions, _entryStateActions, _exitStateActions,
                _logger);
        }



    }
}
