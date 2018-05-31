using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using AvsFilterNet;

namespace AutoOverlay
{
    public static class OverlayUtils
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

        public static Clip InvokeClip(this ScriptEnvironment env, string function, params object[] args)
        {
            var avsArgs = args.Select(p => p.ToAvsValue()).ToArray();
            var avsArg = new AVSValue(avsArgs);
            var res = env.Invoke(function, avsArg);
            return res.AsClip();
        }

        public static Clip InvokeClip(this ScriptEnvironment env, string function, IOrderedDictionary args)
        {
            var avsArgs = args.Values.Cast<object>().Select(p => p.ToAvsValue()).ToArray();
            var avsArg = new AVSValue(avsArgs);
            var res = env.Invoke(function, avsArg, args.Keys.Cast<object>().Select(p => p as string).ToArray());
            return res.AsClip();
        }

        public static AVSValue ToAvsValue(this object val)
        {
            if (val == null)
                return new AVSValue();
            if (val is DynamicEnviroment)
                val = ((DynamicEnviroment) val).Clip;
            if (val is Clip clip)
            {
                var cached = DynamicEnviroment.FindClip(clip);
                if (cached != null)
                    return cached;
            }
            var ctor = typeof(AVSValue).GetConstructor(new[] {val.GetType()});
            if (ctor == null)
                throw new AvisynthException($"Wrong type '{val.GetType()}' for {nameof(AVSValue)} instantiation");
            return (AVSValue) ctor.Invoke(new[] {val});
        }

        public static dynamic Dynamic(this Clip clip)
        {
            return new DynamicEnviroment(clip);
        }

        public static bool IsRealPlanar(Clip clip)
        {
            var colorInfo = clip.GetVideoInfo().pixel_type;
            return colorInfo.HasFlag(ColorSpaces.CS_PLANAR) && !colorInfo.HasFlag(ColorSpaces.CS_INTERLEAVED); //Y8 is interleaved
        }

        public static void ResetChroma(VideoFrame frame)
        {
            MemSet(frame.GetWritePtr(YUVPlanes.PLANAR_U), 128, frame.GetHeight(YUVPlanes.PLANAR_U) * frame.GetPitch(YUVPlanes.PLANAR_U));
            MemSet(frame.GetWritePtr(YUVPlanes.PLANAR_V), 128, frame.GetHeight(YUVPlanes.PLANAR_V) * frame.GetPitch(YUVPlanes.PLANAR_V));
        }

        public static void CopyPlane(VideoFrame from, VideoFrame to, YUVPlanes plane = default(YUVPlanes))
        {
            if (from.GetPitch(plane) == to.GetPitch(plane))
                CopyMemory(to.GetWritePtr(plane), from.GetReadPtr(plane),
                    from.GetHeight(plane) * from.GetRowSize(plane));
            else
            {
                var src = from.GetReadPtr();
                var dest = to.GetWritePtr();
                for (var y = 0; y < from.GetHeight(plane); y++, src += from.GetPitch(plane), dest+=to.GetPitch(plane))
                    CopyMemory(dest, src, from.GetRowSize(plane));
            }
        }

        public static double StdDev(VideoFrame frame)
        {
            var height = frame.GetHeight();
            var rowSize = frame.GetRowSize();
            var rowOffset = frame.GetPitch() - rowSize;
            long sum = 0;
            long squareSum = 0;
            unsafe
            {
                var data = (byte*)frame.GetReadPtr();
                for (var y = 0; y < height; y++, data += rowOffset)
                {
                    for (var x = 0; x < rowSize; x++)
                    {
                        var val = data[x];
                        sum += val;
                        squareSum += val * val;
                    }
                }
                double valCount = rowSize * height;
                var mean = sum / valCount;
                var variance = squareSum / valCount - mean * mean;
                return Math.Sqrt(variance);
            }
        }

        public static void SafeInvoke<T>(this T control, Action<T> action) where T : ISynchronizeInvoke
        {
            if (control.InvokeRequired)
                control.Invoke(action, new object[] { control });
            else
                action(control);
        }

        public static int GetWidthSubsample(ColorSpaces colorSpace)
        {
            return colorSpace.HasFlag(ColorSpaces.CS_Sub_Width_1) ? 1 :
                (colorSpace.HasFlag(ColorSpaces.CS_Sub_Width_4) ? 4 : 2);
        }

        public static int GetHeightSubsample(ColorSpaces colorSpace)
        {
            return colorSpace.HasFlag(ColorSpaces.CS_Sub_Height_1) ? 1 :
                (colorSpace.HasFlag(ColorSpaces.CS_Sub_Height_4) ? 4 : 2);
        }

        public static YUVPlanes[] GetPlanes(ColorSpaces colorSpace)
        {
            if (colorSpace.HasFlag(ColorSpaces.CS_INTERLEAVED))
                return new[] {default(YUVPlanes)};
            return new[] { YUVPlanes.PLANAR_Y, YUVPlanes.PLANAR_U, YUVPlanes.PLANAR_V };
        }

        public static Bitmap ToBitmap(this VideoFrame frame, PixelFormat pixelFormat)
        {
            Bitmap bmp;
            switch (pixelFormat)
            {
                case PixelFormat.Format8bppIndexed:
                    bmp = new Bitmap(frame.GetRowSize(), frame.GetHeight(), frame.GetPitch(), pixelFormat, frame.GetReadPtr());
                    var palette = bmp.Palette;
                    for (var i = 0; i < 256; i++)
                        palette.Entries[i] = Color.FromArgb((byte)i, (byte)i, (byte)i);
                    bmp.Palette = palette;
                    break;
                case PixelFormat.Format24bppRgb:
                    bmp = new Bitmap(frame.GetRowSize()/3, frame.GetHeight(), frame.GetPitch(), pixelFormat, frame.GetReadPtr());
                    bmp.RotateFlip(RotateFlipType.Rotate180FlipX);
                    break;
                default:
                    throw new NotImplementedException();
            }
            return bmp;
        }
    }
}
