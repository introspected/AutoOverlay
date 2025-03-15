﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Threading;
using AutoOverlay.Overlay;
using AvsFilterNet;

namespace AutoOverlay.AviSynth
{
    public class DynamicEnvironment : DynamicObject, IDisposable
    {
        public static string LastError { get; set; }

        static DynamicEnvironment()
        {
            contexts = new ThreadLocal<Stack<DynamicEnvironment>>(() => new Stack<DynamicEnvironment>());
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
        }

        private static readonly ThreadLocal<Stack<DynamicEnvironment>> contexts;

        private readonly Dictionary<Key, Tuple<AVSValue, Clip, object>> cache;

        private readonly ScriptEnvironment _env;

        private object owner;

        private readonly HashSet<object> owners;

        public static dynamic Current => Env;

        public static DynamicEnvironment Env => contexts.Value.Any() ? contexts.Value.Peek() : null;

        private readonly AVSValueCollector collector;

        private bool detached;

        public static AVSValue FindClip(Clip clip)
        {
            return Env?.cache?.Values.FirstOrDefault(p => p.Item2 == clip)?.Item1;
        }

        public static IEnumerable<object> Owners => Env.owners;

        public static void SetOwner(object owner)
        {
            Env.owner = owner;
            if (owner != null)
                Env.owners.Add(owner);
        }

        public static void OwnerExpired(object owner)
        {
            if (owner == null) return;
            Env.cache
                .Where(p => p.Value.Item3 == owner)
                .ToList()
                .ForEach(pair =>
                {
                    pair.Value.Item1.Dispose();
                    pair.Value.Item2?.Dispose();
                    Env.cache.Remove(pair.Key);
                });
            Env.owners.Remove(owner);
        }

        ~DynamicEnvironment()
        {
            DisposeImpl();
        }

        public DynamicEnvironment Attach()
        {
            if (!detached) return this;
            detached = false;
            contexts.Value.Push(this);
            return this;
        }

        public DynamicEnvironment Detach()
        {
            if (detached) return this;
            detached = true;
            return contexts.Value.Pop();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            DisposeImpl();
        }

        public void DisposeImpl()
        {
            if (Clip != null)
                return;
            if (detached)
            {
                DisposeInstance();
                return;
            }
            while (contexts.IsValueCreated && contexts.Value.Count > 0)
            {
                var ctx = Detach();
                ctx.DisposeInstance();

                if (ctx == this)
                    break;
            }
        }

        protected void DisposeInstance()
        {
            owner = null;
            owners.Clear();
            foreach (var val in cache.Values)
            {
                val.Item2?.Dispose();
                val.Item1.Dispose();
            }
            cache.Clear();
            collector?.Dispose();
        }

        public Clip Clip { get; }

        public DynamicEnvironment(ScriptEnvironment env, bool collected = true)
        {
            cache = new();
            owners = [];
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

        public VideoFrame this[int n] => Clip?.GetFrame(n, Env);

        public VideoFrame GetFrame(int n, ScriptEnvironment env)
        {
            return Clip.GetFrame(n, env);
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            var function = string.Intern(binder.Name.ToLower());
            switch (function)
            {
                case "roi":
                    if (args.Length == 1 && args.First() is Rectangle roi)
                    {
                        result = Clip.ROI(roi).Dynamic();
                        return true;
                    }
                    break;
                case "dynamic":
                    result = this;
                    return true;
                case "invoke":
                    function = (string)args[0];
                    args = args.Skip(1).ToArray();
                    break;
                case "dispose":
                    Clip.Dispose();
                    result = this;
                    return true;
            }
            args = PrepareArgs(args).ToArray();

            if (Clip != null)
                args = Enumerable.Repeat(Clip, 1).Concat(args).ToArray();
            var argNames = new string[args.Length];
            for (int lag = args.Length - binder.CallInfo.ArgumentNames.Count, i = 0;
                i < binder.CallInfo.ArgumentNames.Count; i++)
                argNames[lag + i] = binder.CallInfo.ArgumentNames[i];
            argNames = argNames.Where((p, i) => args[i] != null).ToArray();
            args = args.Where(p => p != null).ToArray();
            var key = new Key(function, args, argNames);

#if DEBUG && TRACE
            Func<string> printArgs = () =>
            {
                return string.Join(", ",
                    argNames.Select((n, i) => new
                        {
                            Name = n,
                            Value = args[i] is Clip ? $"Clip@{args[i].GetHashCode()}" :
                                args[i] is IEnumerable ar && !(args[i] is string) ? $"[{string.Join(", ", ar.OfType<object>())}]" :
                                args[i]
                        })
                        .Select(p => p.Name == null ? p.Value : $"{p.Name} = {p.Value}"));
            };
#endif

            if (!Env.cache.TryGetValue(key, out var tuple))
            {
                var avsArgList = args.Select(p => p.ToAvsValue()).ToArray();
                using var avsArgs = new AVSValue(avsArgList);
                var res = ((ScriptEnvironment) Env).Invoke(function, avsArgs, argNames);

                res = ((ScriptEnvironment)Env).Invoke("InternalCache", res);

                var clip = res.AsClip();
#if DEBUG && TRACE
                Debug.WriteLine($"New clip cached @{clip?.GetHashCode()} {function}({printArgs()})");
#endif
                Env.cache[key] = tuple = Tuple.Create(res, clip, Env.owner);
            }
            else
            {
#if DEBUG && TRACE
                Debug.WriteLine($"Cached clip found @{tuple.Item2?.GetHashCode()} {function}({printArgs()})");
#endif
            }

            if (binder.ReturnType == typeof(AVSValue))
                result = tuple.Item1;
            else if (binder.ReturnType == typeof(Clip))
                result = tuple.Item2;
            else if (binder.ReturnType == typeof(object))
                result = new DynamicEnvironment(tuple.Item2);
            else if (binder.ReturnType == typeof(int))
                result = tuple.Item1.AsInt();
            else if (binder.ReturnType == typeof(double))
                result = tuple.Item1.AsFloat();
            else if (binder.ReturnType == typeof(string))
                result = tuple.Item1.AsString();
            else if (binder.ReturnType == typeof(bool))
                result = tuple.Item1.AsBool(false);
            else throw new InvalidOperationException();

            return true;
        }

        private static IEnumerable<object> PrepareArgs(object[] args)
        {
            foreach (var arg in args)
            {
                switch (arg)
                {
                    case DynamicEnvironment dyn:
                        yield return dyn.Clip;
                        break;
                    case IEnumerable<object> collection:
                        foreach (var o in collection)
                            yield return o;
                        break;
                    case Point point:
                        yield return point.X;
                        yield return point.Y;
                        break;
                    case PointF point:
                        yield return point.X;
                        yield return point.Y;
                        break;
                    case Space space:
                        yield return space.X;
                        yield return space.Y;
                        break;
                    case Rectangle rect:
                        yield return rect.Left;
                        yield return rect.Top;
                        yield return rect.Right;
                        yield return rect.Bottom;
                        break;
                    case RectangleD rect:
                        yield return rect.Left;
                        yield return rect.Top;
                        yield return rect.Right;
                        yield return rect.Bottom;
                        break;
                    case RectangleF rect:
                        yield return rect.Left;
                        yield return rect.Top;
                        yield return rect.Right;
                        yield return rect.Bottom;
                        break;
                    case Size size:
                        yield return size.Width;
                        yield return size.Height;
                        break;
                    case SizeD size:
                        yield return size.Width;
                        yield return size.Height;
                        break;
                    default:
                        yield return arg;
                        break;
                }
            }
        }

        public static implicit operator Clip(DynamicEnvironment clip) => clip.Clip;

        public static implicit operator ScriptEnvironment(DynamicEnvironment env) => env?._env;

        public sealed class Key
        {
            private readonly string _function;
            private readonly List<long> _args = new();
            private readonly string[] _argNames;

            public Key(string function, object[] args, string[] argNames)
            {
                _function = function;
                foreach (var arg in args)
                    _args.AddRange(Parse(arg));
                _argNames = argNames;
            }

            private static IEnumerable<long> Parse(object arg)
            {
                switch (arg)
                {
                    case string str:
                        yield return string.Intern(str).GetHashCode();
                        break;
                    case Clip clip:
                        yield return clip.GetHashCode();
                        break;
                    case double real:
                        yield return (long) (real * 1000000000000);
                        break;
                    case IEnumerable col:
                        foreach (var val in col)
                            foreach (var parsed in Parse(val))
                                yield return parsed;
                        break;
                    default:
                        yield return arg.GetHashCode();
                        break;
                }
            }

            private bool Equals(Key other)
            {
                return string.Equals(_function, other._function)
                       && _args.SequenceEqual(other._args)
                       && _argNames.SequenceEqual(other._argNames);
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
                        hash = hash * 23 + item.GetHashCode();
                    foreach (var item in _argNames)
                        hash = hash * 23 + ((item != null) ? item.GetHashCode() : 0);
                    return hash;
                }
            }
        }
    }
}
