using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.StateMachine
{
    public class ConcurrentStateMachine<TState, TInput> : IDisposable
        where TInput : struct, IComparable
        where TState : struct, IComparable
    {
        readonly ILogger _logger;
        readonly string _name;
        readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        readonly Dictionary<TInput, List<IActionWrapper<TState>>> _inputActions;
        readonly Dictionary<TState, List<IActionWrapper<TState>>> _entryStateActions;
        readonly Dictionary<TState, List<IActionWrapper<TState>>> _exitStateActions;


        TState _state;
        bool _disposed;

        public TState State => _state;

        internal ConcurrentStateMachine(string name, TState initialState,
            Dictionary<TInput, List<IActionWrapper<TState>>> inputDescriptors,
            Dictionary<TState, List<IActionWrapper<TState>>> entryStateDescriptors,
            Dictionary<TState, List<IActionWrapper<TState>>> exitStateDescriptors,
            ILogger logger)
        {
            _name = name;
            _state = initialState;
            _inputActions = inputDescriptors;
            _entryStateActions = entryStateDescriptors;
            _exitStateActions = exitStateDescriptors;
            _logger = logger;
        }

        public void Dispose()
        {
            _disposed = true;
            _lock.Wait();
            _lock.Dispose();
        }

        public async Task FireAsync(TInput input)
        {
            //_logger?.LogInformation("FireAsync =====> '{input}', State: {state}", input, _state);
            if (_disposed) throw new ObjectDisposedException(_name);

            TState oldState, newState;

            try
            {
                await _lock.WaitAsync();

                oldState = _state;
                var actionList = _inputActions[input];

                foreach (var action in actionList)
                {
                    if (action.InState == null ||
                        action.InState.Value.CompareTo(oldState) == 0)
                    {
                        if (action.SpecialAction == SpecialAction.SetState)
                            SetState(action.StateToSet);
                        else
                            await action.ExecuteAsync();
                    }
                }

                newState = _state;
            }
            finally
            {
                _lock.Release();
            }

            if (oldState.CompareTo(newState) != 0)
            {
                await ExecuteExitActionsAsync(oldState);
                await ExecuteEntryActionsAsync(newState);
            }
        }

        public void Fire(TInput input)
        {
            //_logger?.LogInformation("Fire =====> '{input}', State: {state}", input, _state);
            if (_disposed) throw new ObjectDisposedException(_name);

            TState oldState, newState;

            try
            {
                _lock.Wait();

                oldState = _state;
                var actionList = _inputActions[input];

                foreach (var action in actionList)
                {
                    if (action.InState == null ||
                        action.InState.Value.CompareTo(oldState) == 0)
                    {
                        if (action.SpecialAction == SpecialAction.SetState)
                            SetState(action.StateToSet);
                        else
                            action.Execute();
                    }
                }

                newState = _state;
            }
            finally
            {
                _lock.Release();
            }

            if (oldState.CompareTo(newState) != 0)
            {
                ExecuteExitActions(oldState);
                ExecuteEntryActions(newState);
            }
        }

        void ExecuteEntryActions(TState state)
        {
            var actionList = _entryStateActions[state];

            foreach (var action in actionList)
            {
                action.Execute();
            }
        }

        void ExecuteExitActions(TState state)
        {
            var actionList = _exitStateActions[state];

            foreach (var action in actionList)
            {
                action.Execute();
            }
        }

        async Task ExecuteEntryActionsAsync(TState state)
        {
            var actionList = _entryStateActions[state];

            foreach (var action in actionList)
            {
                await action.ExecuteAsync();
            }
        }

        async Task ExecuteExitActionsAsync(TState state)
        {
            var actionList = _exitStateActions[state];

            foreach (var action in actionList)
            {
                await action.ExecuteAsync();
            }
        }

        void SetState(TState newState)
        {
            if (_state.CompareTo(newState) != 0)
            {
                _logger?.LogInformation("SM '{name}' has changed state from: [{fromState}] to: [{toState}]",
                    _name, _state, newState);
                _state = newState;
            }
        }

    }
}
