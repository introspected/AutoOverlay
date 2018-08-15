using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Threading;
using AvsFilterNet;

namespace AutoOverlay
{
    public class DynamicEnviroment : DynamicObject, IDisposable
    {
        static DynamicEnviroment()
        {
#if DEBUG
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
#endif
        }

        private static readonly ThreadLocal<Stack<DynamicEnviroment>> contexts = new ThreadLocal<Stack<DynamicEnviroment>>(() => new Stack<DynamicEnviroment>());

        private readonly ConcurrentDictionary<Key, Tuple<AVSValue, Clip>> cache = new ConcurrentDictionary<Key, Tuple<AVSValue, Clip>>();

        private readonly ScriptEnvironment _env;

        public static DynamicEnviroment Env => contexts.Value.Any() ? contexts.Value.Peek() : null;

        private readonly AVSValueCollector collector;

        public static AVSValue FindClip(Clip clip)
        {
            return Env?.cache.Values.FirstOrDefault(p => p.Item2 == clip)?.Item1;
        }

        ~DynamicEnviroment()
        {
            DisposeImpl();
        }

        public void Dispose()
        {
            DisposeImpl();
            GC.SuppressFinalize(this);
        }

        public void DisposeImpl()
        {
            if (Clip != null)
                return;
            while (contexts.Value.Count > 0)
            {
                var ctx = contexts.Value.Pop();
                foreach (var val in ctx.cache.Values)
                {
                    val.Item1.Dispose();
                    val.Item2?.Dispose();
                }
                ctx.cache.Clear();
                ctx.collector.Dispose();
                if (ctx == this)
                    break;
            }
        }

        public Clip Clip { get; }

        public DynamicEnviroment(ScriptEnvironment env)
        {
            contexts.Value.Push(this);
            _env = env;
            collector = new AVSValueCollector();
        }

        internal DynamicEnviroment(Clip clip)
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
            if (function.ToLower().Equals("invoke"))
            {
                function = (string) args[0];
                args = args.Skip(1).ToArray();
            }
            args = args.Select(p => p is DynamicEnviroment ? ((DynamicEnviroment)p).Clip : p).ToArray();
            if (Clip != null)
                args = Enumerable.Repeat(Clip, 1).Concat(args).ToArray();
            var argNames = new string[args.Length];
            for (int lag = args.Length - binder.CallInfo.ArgumentNames.Count, i = 0;
                i < binder.CallInfo.ArgumentNames.Count; i++)
                argNames[lag + i] = binder.CallInfo.ArgumentNames[i];
            argNames = argNames.Where((p, i) => args[i] != null).ToArray();
            var tuple = Env.cache.GetOrAdd(new Key(function, args, argNames),
                key =>
                {
                    var avsArgList = args.Where(p => p != null).Select(p => p.ToAvsValue()).ToArray();
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
                result = new DynamicEnviroment(tuple.Item2);
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

        public static implicit operator Clip(DynamicEnviroment clip) => clip.Clip;

        public static implicit operator ScriptEnvironment(DynamicEnviroment env) => env._env;

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
