using System;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.StateMachine
{
    internal enum SpecialAction
    {
        None,
        SetState,
    }

    internal interface IActionWrapper<TState>
        where TState : struct, IComparable
    {
        TState? InState { get; }
        SpecialAction SpecialAction { get; }
        TState StateToSet { get; }

        void Execute();
        Task ExecuteAsync();
    }

    internal class SetStateActionWrapper<TState> : IActionWrapper<TState>
        where TState : struct, IComparable
    {
        readonly TState? _inState;
        readonly TState _stateToSet;
        readonly SpecialAction _specialAction;

        public TState? InState => _inState;
        public TState StateToSet => _stateToSet;
        public SpecialAction SpecialAction => _specialAction;

        public SetStateActionWrapper(SpecialAction specialAction, TState stateToSet, TState? inState)
        {
            _specialAction = specialAction;
            _stateToSet = stateToSet;
            _inState = inState;
        }

        public void Execute()
        {
            throw new NotImplementedException();
        }

        public Task ExecuteAsync()
        {
            throw new NotImplementedException();
        }
    }

    internal class ActionWrapper<TState> : IActionWrapper<TState>
        where TState : struct, IComparable
    {
        readonly object _action;
        readonly TState? _inState;

        public TState? InState => _inState;
        public TState StateToSet => throw new NotImplementedException();
        public SpecialAction SpecialAction => SpecialAction.None;

        public ActionWrapper(object action, TState? inState)
        {
            _action = action;
            _inState = inState;
        }

        public void Execute()
        {
            if (_action is Action action)
                action();
            else if (_action is Func<Task>)
                throw new InvalidOperationException("This transition is configured to use awaitable actions. Use FireAsync() instead.");
        }

        public Task ExecuteAsync()
        {
            if (_action is Action action)
                action();
            else if (_action is Func<Task> task)
                return task();

            return Task.CompletedTask;
        }
    }


}
