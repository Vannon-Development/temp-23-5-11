using UnityEngine;
using UnityEngine.InputSystem;

namespace Character
{
    public class CharacterBase : MonoBehaviour
    {
        public float walkSpeed;
        public float jumpForce;
        public Transform direction;
        
        private StateContext _context;

        private static readonly int MoveModeHash = Animator.StringToHash("MoveMode");
        private static readonly int JumpHash = Animator.StringToHash("Jump");

        private void Start()
        {
            _context = new StateContext()
            {
                Ani = GetComponent<Animator>(),
                Body = GetComponent<Rigidbody2D>(),
                WalkSpeed = walkSpeed,
                Direction = direction,
                JumpForce = jumpForce
            };
            _context.CurrentState = _context.States[(int)StateContext.StateName.Idle];
            _context.CurrentState.Begin(_context);
        }

        private void OnMove(InputValue value)
        {
            _context.CurrentState.SetMoveStick(value.Get<Vector2>());
        }

        private void OnJump(InputValue value)
        {
            if (value.Get<float>().NearZero())
                _context.JumpHeld = false;
            else
                _context.CurrentState.Jump();
        }

        private void FixedUpdate()
        {
            _context.Body.angularVelocity = 0;
            _context.Body.velocity = new Vector2(_context.ControlledVelocity, _context.Body.velocity.y);
            if(_context.Body.velocity.y.NearZero())
                _context.CurrentState.Grounded();
            else
                _context.CurrentState.Fall();
        }

        private void JumpAniFinished()
        {
            _context.JumpFinished = true;
            _context.CurrentState.Fall();
        }

        private class StateContext
        {
            public State CurrentState;
            public Animator Ani;
            public Rigidbody2D Body;
            public Vector2 MoveInput;
            public float WalkSpeed;
            public float ControlledVelocity;
            public Transform Direction;
            public float JumpForce;
            public bool JumpFinished;
            public bool JumpHeld;

            public readonly State[] States =
            {
                new IdleState(),
                new WalkingState(),
                new JumpState(),
                new FallState()
            };
            
            public enum StateName { Idle, Walking, Jump, Fall }
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

            public virtual void Jump() { }

            public virtual void Fall()
            {
                ChangeState(StateContext.StateName.Fall);
            }

            public virtual void Grounded() { }
        }

        private class IdleState : State
        {
            public override void Begin(StateContext context)
            {
                base.Begin(context);
                Context.Ani.SetInteger(MoveModeHash, 1);
                Context.ControlledVelocity = 0;
            }

            public override void SetMoveStick(Vector2 value)
            {
                base.SetMoveStick(value);
                if(!Context.MoveInput.x.NearZero())
                    ChangeState(StateContext.StateName.Walking);
            }

            public override void Jump()
            {
                base.Jump();
                ChangeState(StateContext.StateName.Jump);
            }
        }

        private class WalkingState : State
        {
            public override void Begin(StateContext context)
            {
                base.Begin(context);
                Context.Ani.SetInteger(MoveModeHash, 2);
                Set();
            }

            public override void SetMoveStick(Vector2 value)
            {
                base.SetMoveStick(value);
                if(Context.MoveInput.x.NearZero())
                    ChangeState(StateContext.StateName.Idle);
                else
                    Set();
            }

            public override void Jump()
            {
                base.Jump();
                ChangeState(StateContext.StateName.Jump);
            }

            private void Set()
            {
                Context.ControlledVelocity = Context.WalkSpeed * Context.MoveInput.x;
                Context.Direction.localScale = Context.MoveInput.x < 0 ? new Vector3(-1, 1, 1) : new Vector3(1, 1, 1);
            }
        }

        private abstract class InAirState : State
        {
            private float _initDir;
            private bool _low;
            
            public override void Begin(StateContext context)
            {
                base.Begin(context);
                _initDir = Context.MoveInput.x;
                _low = !_initDir.NearZero();
                print(_low);
            }

            public override void SetMoveStick(Vector2 value)
            {
                base.SetMoveStick(value);
                _low = _low || !(Mathf.Sign(_initDir) - Mathf.Sign(Context.MoveInput.x)).NearZero();
                Context.ControlledVelocity = Context.WalkSpeed * Context.MoveInput.x * (_low ? 0.6f : 1.0f);
                if(!Context.ControlledVelocity.NearZero() && !(Mathf.Sign(Context.ControlledVelocity) - Mathf.Sign(Context.Direction.localScale.x)).NearZero())
                    Context.Direction.localScale = Context.MoveInput.x < 0 ? new Vector3(-1, 1, 1) : new Vector3(1, 1, 1);
            }
        }

        private class JumpState : InAirState
        {
            public override void Begin(StateContext context)
            {
                base.Begin(context);
                Context.Ani.SetInteger(MoveModeHash, 0);
                Context.Ani.SetTrigger(JumpHash);
                Context.Body.AddForce(new Vector2(0, Context.JumpForce), ForceMode2D.Impulse);
                Context.JumpFinished = false;
            }

            public override void Fall()
            {
                if(Context.JumpFinished)
                    base.Fall();
            }
        }

        private class FallState : InAirState
        {
            public override void Begin(StateContext context)
            {
                base.Begin(context);
                Context.Ani.SetInteger(MoveModeHash, 3);
            }

            public override void Fall() { }

            public override void Grounded()
            {
                base.Grounded();
                ChangeState(Context.MoveInput.x.NearZero()
                    ? StateContext.StateName.Idle
                    : StateContext.StateName.Walking);
            }
        }
    }
}