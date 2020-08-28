using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Threading;
using AvsFilterNet;

namespace AutoOverlay
{
    public class DynamicEnvironment : DynamicObject, IDisposable
    {
        static DynamicEnvironment()
        {
#if DEBUG
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
#endif
        }

        private static readonly ThreadLocal<Stack<DynamicEnvironment>> contexts = new ThreadLocal<Stack<DynamicEnvironment>>(() => new Stack<DynamicEnvironment>());

        private readonly ConcurrentDictionary<Key, Tuple<AVSValue, Clip>> cache = new ConcurrentDictionary<Key, Tuple<AVSValue, Clip>>();

        private readonly ScriptEnvironment _env;

        public static DynamicEnvironment Env => contexts.Value.Any() ? contexts.Value.Peek() : null;

        private readonly AVSValueCollector collector;

        private bool detached;

        public static AVSValue FindClip(Clip clip)
        {
            return Env?.cache?.Values.FirstOrDefault(p => p.Item2 == clip)?.Item1;
        }

        ~DynamicEnvironment()
        {
            //DisposeImpl();
        }

        public DynamicEnvironment Detach()
        {
            if (detached) return this;
            detached = true;
            return contexts.Value.Pop();
        }

        public void Dispose()
        {
            //GC.SuppressFinalize(this);
            DisposeImpl();
        }

        public void DisposeImpl()
        {
            if (Clip != null)
                return;
            while (contexts.Value.Count > 0)
            {
                var ctx = Detach();
                foreach (var val in ctx.cache.Values)
                {
                    val.Item1.Dispose();
                    val.Item2?.Dispose();
                }
                ctx.cache.Clear();
                ctx.collector?.Dispose();

                if (ctx == this)
                    break;
            }
        }

        public Clip Clip { get; }

        public DynamicEnvironment(ScriptEnvironment env, bool collected = true)
        {
            contexts.Value.Push(this);
            _env = env;
            if (collected)
            {
                collector = new AVSValueCollector();
            }
        }

        internal DynamicEnvironment(Clip clip)
        {
            Clip = clip;
        }

        public VideoFrame this[int n] => Clip.GetFrame(n, Env);

        public VideoFrame GetFrame(int n, ScriptEnvironment env)
        {
            return Clip.GetFrame(n, env);
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            var function = binder.Name;
            if (function.Equals("Dynamic"))
            {
                result = this;
                return true;
            }
            if (function.ToLower().Equals("invoke"))
            {
                function = (string) args[0];
                args = args.Skip(1).ToArray();
            }

            args = PrepareArgs(args);

            if (Clip != null)
                args = Enumerable.Repeat(Clip, 1).Concat(args).ToArray();
            var argNames = new string[args.Length];
            for (int lag = args.Length - binder.CallInfo.ArgumentNames.Count, i = 0;
                i < binder.CallInfo.ArgumentNames.Count; i++)
                argNames[lag + i] = binder.CallInfo.ArgumentNames[i];
            argNames = argNames.Where((p, i) => args[i] != null).ToArray();
            args = args.Where(p => p != null).ToArray();
            var tuple = Env.cache.GetOrAdd(new Key(function, args, argNames),
                key =>
                {
                    var avsArgList = args.Select(p => p.ToAvsValue()).ToArray();
                    var avsArgs = new AVSValue(avsArgList);
                    var res = ((ScriptEnvironment) Env).Invoke(function, avsArgs, argNames);
                    var clip = res.AsClip();
                    clip?.SetCacheHints(CacheType.CACHE_25_ALL, 1);
                    return new Tuple<AVSValue, Clip>(res, clip);
                });

            if (binder.ReturnType == typeof(AVSValue))
                result = tuple.Item1;
            else if (binder.ReturnType == typeof(Clip))
                result = tuple.Item2;
            else if (binder.ReturnType == typeof(object))
                result = new DynamicEnvironment(tuple.Item2);
            else if (binder.ReturnType == typeof(int))
                result = tuple.Item1.AsInt();
            else if (binder.ReturnType == typeof(float))
                result = tuple.Item1.AsFloat();
            else if (binder.ReturnType == typeof(string))
                result = tuple.Item1.AsString();
            else if (binder.ReturnType == typeof(bool))
                result = tuple.Item1.AsBool(false);
            else throw new InvalidOperationException();

            return true;
        }

        private static object[] PrepareArgs(object[] args)
        {
            var realArgs = new List<object>();
            foreach (var arg in args)
            {
                switch (arg)
                {
                    case IEnumerable<object> collection:
                        realArgs.AddRange(collection);
                        break;
                    case Point point:
                        realArgs.Add(point.X);
                        realArgs.Add(point.Y);
                        break;
                    case RectangleD rect:
                        realArgs.Add(rect.Left);
                        realArgs.Add(rect.Top);
                        realArgs.Add(rect.Right);
                        realArgs.Add(rect.Bottom);
                        break;
                    case Size size:
                        realArgs.Add(size.Width);
                        realArgs.Add(size.Height);
                        break;
                    case Clip clip:
                        realArgs.Add(clip);
                        break;
                    default:
                        realArgs.Add(arg);
                        break;
                }
            }
            return realArgs.ToArray();
        }

        public static implicit operator Clip(DynamicEnvironment clip) => clip.Clip;

        public static implicit operator ScriptEnvironment(DynamicEnvironment env) => env?._env;

        class Key
        {
            private readonly string _function;
            private readonly object[] _args;
            private readonly string[] _argNames;

            public Key(string function, object[] args, string[] argNames)
            {
                _function = function;
                _args = args;
                _argNames = argNames;
            }

            private bool Equals(Key other)
            {
                return string.Equals(_function, other._function)
                       && Enumerable.SequenceEqual(_args, other._args)
                       && Enumerable.SequenceEqual(_argNames, other._argNames);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Key) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = _function.GetHashCode() * 23;
                    foreach (var item in _args)
                        hash = hash * 23 + ((item != null) ? item.GetHashCode() : 0);
                    foreach (var item in _argNames)
                        hash = hash * 23 + ((item != null) ? item.GetHashCode() : 0);
                    return hash;
                }
            }
        }
    }
}
