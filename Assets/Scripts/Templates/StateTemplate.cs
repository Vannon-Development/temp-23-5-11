using UnityEngine;

namespace Templates
{
    public class StateTemplate : MonoBehaviour
    {
        private StateContext _context;

        private void Start()
        {
            _context = new StateContext();
            _context.CurrentState = _context.States[(int)StateContext.StateName.State1];
            _context.CurrentState.Begin(_context);
        }

        private class StateContext
        {
            public State CurrentState;
            
            public readonly State[] States =
            {
                new State1(),
                new State2()
            };
            
            public enum StateName { State1, State2 }

        }

        private abstract class State
        {
            protected StateContext Context;

            public virtual void Begin(StateContext context)
            {
                Context = context;
            }
            
            protected void ChangeState(StateContext.StateName state)
            {
                Context.CurrentState = Context.States[(int)state];
                Context.CurrentState.Begin(Context);
            }
        }

        private class State1 : State
        {
            
        }

        private class State2 : State
        {
            
        }
    }
}
