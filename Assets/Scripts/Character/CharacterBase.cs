using UnityEngine;
using UnityEngine.InputSystem;

namespace Character
{
    public class CharacterBase : MonoBehaviour
    {
        private StateContext _context;

        private static readonly int WalkingHash = Animator.StringToHash("Walking");

        private void Start()
        {
            _context = new StateContext()
            {
                Ani = GetComponent<Animator>()
            };
            _context.CurrentState = _context.States[(int)StateContext.StateName.Idle];
            _context.CurrentState.Begin(_context);
        }

        private void OnMove(InputValue value)
        {
            _context.CurrentState.SetMoveStick(value.Get<Vector2>());
        }
        
        private class StateContext
        {
            public State CurrentState;
            public Animator Ani;
            public Vector2 MoveInput;
            
            public readonly State[] States =
            {
                new IdleState(),
                new WalkingState()
            };
            
            public enum StateName { Idle, Walking }

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

            public virtual void SetMoveStick(Vector2 value)
            {
                Context.MoveInput = value;
            }
        }

        private class IdleState : State
        {
            public override void Begin(StateContext context)
            {
                base.Begin(context);
                Context.Ani.SetBool(WalkingHash, false);
            }

            public override void SetMoveStick(Vector2 value)
            {
                base.SetMoveStick(value);
                if(!Context.MoveInput.magnitude.NearZero())
                    ChangeState(StateContext.StateName.Walking);
            }
        }

        private class WalkingState : State
        {
            public override void Begin(StateContext context)
            {
                base.Begin(context);
                Context.Ani.SetBool(WalkingHash, true);
            }

            public override void SetMoveStick(Vector2 value)
            {
                base.SetMoveStick(value);
                if(Context.MoveInput.magnitude.NearZero())
                    ChangeState(StateContext.StateName.Idle);
            }
        }
    }
}