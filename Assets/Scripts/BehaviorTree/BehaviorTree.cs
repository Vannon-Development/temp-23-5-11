using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BehaviorTree
{
    public class BehaviorTree<T> where T : TreeContext
    {
        private readonly T _context;
        private Node<T> _root;
        private readonly NodeMap<T> _map;
        
        public BehaviorTree(T context)
        {
            _context = context;
            _map = new NodeMap<T>();
        }

        public void SetRoot(Node<T> root)
        {
            _root = root;
        }

        public void SetRoot(String items)
        {
            var lines = items.Split('\n');
            _root = _map.BuildBase(lines, _context);
        }

        public void Tick()
        {
            _root.Tick();
        }

        public void RegisterNode(string name, Type type)
        {
            _map.NodeList[name] = type;
        }
    }
    
    public class NodeMap<T> where T : TreeContext
    {
        public readonly Dictionary<string, Type> NodeList;

        public NodeMap()
        {
            NodeList = new Dictionary<string, Type>
            {
                { "Priority", typeof(PriorityNode<T>) },
                { "Sequence", typeof(SequenceNode<T>) },
                { "Loop", typeof(LoopNode<T>) },
                { "Random", typeof(RandomNode<T>) },
                { "Concurrent", typeof(ConcurrentNode<T>) },
                { "ConvertTo", typeof(ConvertToNode<T>) },
                { "Inverter", typeof(InverterNode<T>) },
                { "Timer", typeof(TimerNode<T>) }
            };
        }

        private int Depth(string line)
        {
            if (line.Length == 0) return -1;
            int depth;
            for (depth = 0; depth < line.Length; depth++)
            {
                var ch = line[depth];
                if (ch != ' ' && ch != '\t')
                    break;
            }

            return depth;
        }

        public Node<T> BuildBase(string[] lines, T context)
        {
            var items = lines[0].Split(' ');
            var name = items[0].Trim();
            var type = NodeList[name];
            if (type == null) throw new Exception($"Node {name} not found");
            string[] param = null;
            if(items.Length > 1)
                param = items[1..];

            if (type.IsSubclassOf(typeof(ParentNode<T>)))
            {
                if (lines.Length < 2) throw new Exception($"Parent node {name} has no children");
                var targetDepth = Depth(lines[1]);
                if (targetDepth <= Depth(lines[0]))
                    throw new Exception($"Parent node {name} has an invalid child depth");
                var children = new List<Node<T>>();

                for (int counter = 1; counter < lines.Length; counter++)
                {
                    var depth = Depth(lines[counter]);
                    if (depth == targetDepth)
                    {
                        children.Add(BuildBase(lines[counter..], context));
                    }
                    else if (depth < targetDepth)
                        break;
                }

                if (param == null)
                    return (ParentNode<T>)Activator.CreateInstance(type, context, children.ToArray());
                return (ParentNode<T>)Activator.CreateInstance(type, context, param, children.ToArray());
            }
            else if (type.IsSubclassOf(typeof(DecoratorNode<T>)))
            {
                if (lines.Length < 2) throw new Exception($"Decorator node {name} has no children");
                var cDepth = Depth(lines[1]);
                if (cDepth <= Depth(lines[0])) throw new Exception($"Decorator node {name} has an invalid child depth");
                if (lines.Length > 2 && Depth(lines[2]) == cDepth)
                    throw new Exception($"Decorator node {name} has too many children");

                Node<T> child = BuildBase(lines[1..], context);
                if (param == null)
                    return (DecoratorNode<T>)Activator.CreateInstance(type, context, child);
                return (DecoratorNode<T>)Activator.CreateInstance(type, context, param, child);
            }
            else if(type.IsSubclassOf(typeof(LeafNode<T>)))
            {
                if (param == null)
                    return (LeafNode<T>)Activator.CreateInstance(type, context);
                return (LeafNode<T>)Activator.CreateInstance(type, context, param);
            }
            else
            {
                throw new Exception($"Node {name} does not inherit a primary type");
            }
        }
    }
    
    public abstract class TreeContext
    {
        
    }

    public enum Status { Running, Success, Failure }
    
    public abstract class Node<T> where T: TreeContext
    {
        protected readonly T Context;

        protected Node(T context)
        {
            Context = context;
        }

        public abstract Status Tick();
    }

    public abstract class ParentNode<T> : Node<T> where T : TreeContext
    {
        protected readonly Node<T>[] Children;

        protected ParentNode(T context, Node<T>[] children) : base(context)
        {
            Children = children;
        }
    }

    public class PriorityNode<T> : ParentNode<T> where T : TreeContext
    {
        private readonly bool _runningFirst;
        private readonly bool _returnOnSucceed;
        private int _runningIndex;

        public PriorityNode(T context, string[] param, Node<T>[] children):
            this(context, bool.Parse(param[0]), bool.Parse(param[1]), children) { }
        public PriorityNode(T context, bool runningFirst, bool returnOnSucceed, Node<T>[] children): base(context, children)
        {
            _runningFirst = runningFirst;
            _returnOnSucceed = returnOnSucceed;
        }

        public override Status Tick()
        {
            var index = 0;
            if (_runningFirst) index = _runningIndex;
            _runningIndex = 0;
            while (index >= 0 && index < Children.Length)
            {
                var result = Children[index].Tick();
                if (_returnOnSucceed && result == Status.Success)
                    return Status.Success;
                else if (!_returnOnSucceed && result == Status.Failure)
                    return Status.Failure;
                else if (result == Status.Running)
                {
                    _runningIndex = index;
                    return Status.Running;
                }

                index = NextChild(index);
            }

            return _returnOnSucceed ? Status.Failure : Status.Success;
        }

        protected virtual int NextChild(int index)
        {
            return index + 1;
        }
    }

    public class SequenceNode<T> : PriorityNode<T> where T : TreeContext
    {
        public SequenceNode(T context, Node<T>[] children) : base(context, true, false, children){}
    }

    public class LoopNode<T> : PriorityNode<T> where T : TreeContext
    {
        public LoopNode(T context, string[] param, Node<T>[] children) :
            this(context, bool.Parse(param[0]), children) { }
        public LoopNode(T context, bool returnOnSucceed, Node<T>[] children) : base(context, true, returnOnSucceed, children) {}

        protected override int NextChild(int index)
        {
            if (index == Children.Length)
                return 0;
            return index + 1;
        }
    }

    public class RandomNode<T> : ParentNode<T> where T : TreeContext
    {
        private int _runningIndex;

        public RandomNode(T context, Node<T>[] children): base(context, children)
        {
            _runningIndex = -1;
        }

        public override Status Tick()
        {
            if (_runningIndex >= 0)
                return Children[_runningIndex].Tick();
            else
            {
                var index = Random.Range(0, Children.Length - 1);
                var result = Children[index].Tick();
                if (result == Status.Running)
                    _runningIndex = index;
                return result;
            }
        }
    }

    public class ConcurrentNode<T> : ParentNode<T> where T : TreeContext
    {
        private readonly bool _countSuccess;
        private readonly int _requiredCount;

        public ConcurrentNode(T context, string[] param, Node<T>[] children):
            this(context, bool.Parse(param[0]), int.Parse(param[1]), children) {}
        public ConcurrentNode(T context, bool countSuccess, int requiredCount, Node<T>[] children): base(context, children)
        {
            _countSuccess = countSuccess;
            _requiredCount = requiredCount;
        }

        public override Status Tick()
        {
            var count = 0;
            foreach (var t in Children)
            {
                var result = t.Tick();
                switch (result)
                {
                    case Status.Success when _countSuccess:
                    case Status.Failure when !_countSuccess:
                        count += 1;
                        break;
                }
            }

            return count >= _requiredCount ? Status.Success : Status.Failure;
        }
    }

    public abstract class DecoratorNode<T> : Node<T> where T : TreeContext
    {
        protected readonly Node<T> Child;

        protected DecoratorNode(T context, Node<T> child): base(context)
        {
            Child = child;
        }
    }

    public class ConvertToNode<T> : DecoratorNode<T> where T : TreeContext
    {
        private readonly bool _convertToSuccess;

        public ConvertToNode(T context, string[] param, Node<T> child):
            this(context, bool.Parse(param[0]), child) {}
        public ConvertToNode(T context, bool convertToSuccess, Node<T> child) : base(context, child)
        {
            _convertToSuccess = convertToSuccess;
        }

        public override Status Tick()
        {
            var result = Child.Tick();
            if (result == Status.Running) return Status.Running;
            return _convertToSuccess ? Status.Success : Status.Failure;
        }
    }

    public class InverterNode<T> : DecoratorNode<T> where T : TreeContext
    {
        public InverterNode(T context, Node<T> child): base(context, child) {}

        public override Status Tick()
        {
            var result = Child.Tick();
            if (result == Status.Running) return Status.Running;
            return result == Status.Success ? Status.Failure : Status.Success;
        }
    }

    public class TimerNode<T> : DecoratorNode<T> where T : TreeContext
    {
        private readonly float _target;
        private readonly bool _repeating;
        private float _timer;

        public TimerNode(T context, string[] param, Node<T> child):
            this(context, float.Parse(param[0]), bool.Parse(param[1]), child){}
        public TimerNode(T context, float targetTime, bool repeating, Node<T> child) : base(context, child)
        {
            _target = targetTime;
            _repeating = repeating;
        }

        public override Status Tick()
        {
            if(_timer < _target)
                _timer += Time.deltaTime;
            if (_timer >= _target)
            {
                var result = Child.Tick();
                if (_repeating)
                    _timer = 0;
                return result;
            }

            return Status.Running;
        }
    }

    public abstract class LeafNode<T> : Node<T> where T : TreeContext
    {
        protected LeafNode(T context): base(context) {}
    }
}
