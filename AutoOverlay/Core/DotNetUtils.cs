using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AutoOverlay
{
    public static class DotNetUtils
    {
        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, int count);

        [DllImport("msvcrt.dll", EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        public static extern IntPtr MemSet(IntPtr dest, int c, int count);

        public static IEnumerable<T> Append<T>(this IEnumerable<T> e, T item)
        {
            foreach (var i in e)
                yield return i;
            yield return item;
        }

        public static T Also<T>(this T obj, Action<T> action)
        {
            action(obj);
            return obj;
        }

        public static V Let<T, V>(this T obj, Func<T, V> func) => func(obj);

        public static IEnumerable<T> Enumerate<T>(this T obj)
        {
            yield return obj;
        }

        public static IEnumerable<T> Peek<T>(this IEnumerable<T> enumerable, Action<T> action) => enumerable.Select(p =>
        {
            action(p);
            return p;
        });

        public static IEnumerable<T> Shift<T>(this IEnumerable<T> enumerable, int value)
        {
            using var enumerator = enumerable.GetEnumerator();
            var array = new T[value];
            for (var i = 0; i < array.Length; i++)
            {
                enumerator.MoveNext();
                array[i] = enumerator.Current;
            }

            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }
            foreach (var item in array)
            {
                yield return item;
            }
        }

        public static IEnumerable<(T1, T2)> Merge<T1, T2>(this IEnumerable<T1> first, IEnumerable<T2> second)
        {
            var en1 = first.GetEnumerator();
            var en2 = second.GetEnumerator();
            while (en1.MoveNext() && en2.MoveNext())
                yield return (en1.Current, en2.Current);
        }

        public static void SafeInvoke<T>(this T control, Action<T> action, bool async = true)
            where T : ISynchronizeInvoke
        {
            if (control.InvokeRequired)
                if (async)
                    control.BeginInvoke(action, [control]);
                else control.Invoke(action, [control]);
            else
                action(control);
        }

        public static V SafeInvoke<T, V>(this T control, Func<T, V> func) where T : ISynchronizeInvoke
        {
            if (control.InvokeRequired)
                return (V)control.Invoke(func, [control]);
            else
                return func(control);
        }

        public static object Unbox(this object value)
        {
            var type = value?.GetType();
            if (type is { IsGenericType: true } && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                dynamic nullable = value;
                return nullable.HasValue ? nullable.Value : null;
            }
            return value;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNearlyZero(this double number) => Math.Abs(number) < OverlayConst.EPSILON;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNearlyZero(this float number) => Math.Abs(number) < OverlayConst.EPSILON;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNearlyEquals(this double a, double b, double tolerance = OverlayConst.EPSILON) => Math.Abs(a - b) < tolerance;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNearlyEquals(this float a, float b) => Math.Abs(a - b) < OverlayConst.EPSILON;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Floor(this double value, double tolerance = 0.000001)
        {
            var ceiling = (int) Math.Ceiling(value);
            return ceiling - value < tolerance ? ceiling : ceiling - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Ceiling(this double value, double tolerance = 0.000001)
        {
            var floor = (int)Math.Floor(value);
            return value - floor < tolerance ? floor : floor + 1;
        }
    }
}
