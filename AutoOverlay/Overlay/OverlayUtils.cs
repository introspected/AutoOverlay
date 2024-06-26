﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AutoOverlay.AviSynth;
using AutoOverlay.Overlay;
using AvsFilterNet;

namespace AutoOverlay
{
    public static class OverlayUtils
    {
        public const string DEFAULT_PRESIZE_FUNCTION = "BilinearResize";
        public const string DEFAULT_RESIZE_FUNCTION = "BicubicResize";
        public const string DEFAULT_ROTATE_FUNCTION = "BilinearRotate";
        public const MtMode DEFAULT_MT_MODE = MtMode.SERIALIZED;
        public const int OVERLAY_FORMAT_VERSION = 5;
        public const int ENGINE_HISTORY_LENGTH = 10;
        public const int ENGINE_TOTAL_FRAMES = ENGINE_HISTORY_LENGTH * 2 + 1;

        public const int FRACTION = 7;
        public static readonly double EPSILON = Math.Pow(10, -FRACTION);

        public static readonly Size NO_SUB_SAMPLE = new(1, 1);

        private static readonly Dictionary<int, string> ColorSpaceMap = Enum.GetNames(typeof(ColorSpaces))
            .GroupBy(p => (int)Enum.Parse(typeof(ColorSpaces), p))
            .ToDictionary(p => p.Key, p => p
                .Select(val => val.ToString().Replace("CS_", "").Replace("BGR", "RGB"))
                .First(val => !val.Contains("GENERIC")));

        private static readonly Dictionary<ColorSpaces, int> BitDepths = new()
        {
            { ColorSpaces.CS_Sample_Bits_14, 14 },
            { ColorSpaces.CS_Sample_Bits_12, 12 },
            { ColorSpaces.CS_Sample_Bits_10, 10 },
            { ColorSpaces.CS_Sample_Bits_16, 16 },
            { ColorSpaces.CS_Sample_Bits_32, 32 },
            { ColorSpaces.CS_Sample_Bits_8, 8 }
        };

        private static readonly Dictionary<int, PlaneChannel[]> PlaneChannels = Enum.GetValues(typeof(ColorSpaces))
            .Cast<ColorSpaces>()
            .Select(p => (int)p)
            .Distinct()
            .Select(p => (ColorSpaces)p)
            .ToDictionary(p => (int)p, space => space switch
            {
                ColorSpaces.CS_YUY2 => [
                    new(default, YUVPlanes.PLANAR_Y, 0, 2, 8),
                    new(default, YUVPlanes.PLANAR_U, 1, 4, 8),
                    new(default, YUVPlanes.PLANAR_V, 3, 4, 8)],
                _ when space.HasFlag(ColorSpaces.CS_GENERIC_RGBP) => Planar(space.GetBitDepth(), YUVPlanes.PLANAR_R, YUVPlanes.PLANAR_G, YUVPlanes.PLANAR_B),
                _ when space.HasFlag(ColorSpaces.CS_GENERIC_RGBAP) => Planar(space.GetBitDepth(), YUVPlanes.PLANAR_R, YUVPlanes.PLANAR_G, YUVPlanes.PLANAR_B, YUVPlanes.PLANAR_A),
                _ when space.HasFlag(ColorSpaces.CS_RGB_TYPE | ColorSpaces.CS_INTERLEAVED) => Interleaved(3, space.GetBitDepth(), YUVPlanes.PLANAR_B, YUVPlanes.PLANAR_G, YUVPlanes.PLANAR_R),
                _ when space.HasFlag(ColorSpaces.CS_RGBA_TYPE | ColorSpaces.CS_INTERLEAVED) => Interleaved(4, space.GetBitDepth(), YUVPlanes.PLANAR_B, YUVPlanes.PLANAR_G, YUVPlanes.PLANAR_R, YUVPlanes.PLANAR_A),
                _ when space.HasFlag(ColorSpaces.CS_GENERIC_Y) => Planar(space.GetBitDepth(), YUVPlanes.PLANAR_Y),
                _ when space.HasFlag(ColorSpaces.CS_PLANAR) => Planar(space.GetBitDepth(), YUVPlanes.PLANAR_Y, YUVPlanes.PLANAR_U, YUVPlanes.PLANAR_V),
                _ => Array.Empty<PlaneChannel>()
            });

        public static PlaneChannel[] Interleaved(int channelCount, int depth, params YUVPlanes[] effectivePlanes) => Enumerable.Range(0, channelCount)
            .Select(i => new PlaneChannel(default, effectivePlanes[i], i, channelCount, depth)).ToArray();

        public static PlaneChannel[] Planar(int depth, params YUVPlanes[] planes) =>
            planes.Select(p => new PlaneChannel(p, p, 0, 1, depth)).ToArray();

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, int count);

        [DllImport("msvcrt.dll", EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        public static extern IntPtr MemSet(IntPtr dest, int c, int count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNearlyZero(this double number) => Math.Abs(number) < EPSILON;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNearlyZero(this float number) => Math.Abs(number) < EPSILON;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNearlyEquals(this double a, double b) => Math.Abs(a - b) < EPSILON;

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
            if (val is AVSValue avs)
                return avs;
            if (val is DynamicEnvironment)
                val = ((DynamicEnvironment) val).Clip;
            if (val is Clip clip)
            {
                var cached = DynamicEnvironment.FindClip(clip);
                if (cached != null)
                    return cached;
            }
            if (val is Enum)
                val = val.ToString();

            if (!(val is string) && val is IEnumerable array)
            {
                return new AVSValue(array.OfType<object>().Select(ToAvsValue).ToArray());
            }
            var ctor = typeof(AVSValue).GetConstructor(new[] {val.GetType()});
            if (ctor == null)
                throw new AvisynthException($"Wrong type '{val.GetType()}' for {nameof(AVSValue)} instantiation");
            return (AVSValue) ctor.Invoke(new[] { val });
        }

        public static dynamic Dynamic(this Clip clip) => new DynamicEnvironment(clip);

        public static bool IsRealPlanar(this Clip clip)
        {
            var colorInfo = clip.GetVideoInfo().pixel_type;
            return IsRealPlanar(colorInfo);
        }

        public static bool IsRealPlanar(this ColorSpaces pixelType)
        {
            return pixelType.HasFlag(ColorSpaces.CS_PLANAR) && !pixelType.HasFlag(ColorSpaces.CS_INTERLEAVED); //Y8 is interleaved
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
                    from.GetHeight(plane) * from.GetPitch(plane));
            else
            {
                var src = from.GetReadPtr(plane);
                var dest = to.GetWritePtr(plane);
                for (var y = 0; y < from.GetHeight(plane); y++, src += from.GetPitch(plane), dest += to.GetPitch(plane))
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

        public static void SafeInvoke<T>(this T control, Action<T> action, bool async = true)
            where T : ISynchronizeInvoke
        {
            if (control.InvokeRequired)
                if (async)
                    control.BeginInvoke(action, new object[] { control });
                else control.Invoke(action, new object[] { control });
            else
                action(control);
        }

        public static V SafeInvoke<T, V>(this T control, Func<T, V> func) where T : ISynchronizeInvoke
        {
            if (control.InvokeRequired)
                return (V) control.Invoke(func, new object[] { control });
            else
                return func(control);
        }

        public static int GetWidthSubsample(this ColorSpaces colorSpace)
        {
            if (!colorSpace.IsRealPlanar())
                return 1;
            return colorSpace.HasFlag(ColorSpaces.CS_Sub_Width_1) ? 1 :
                (colorSpace.HasFlag(ColorSpaces.CS_Sub_Width_4) ? 4 : 2);
        }

        public static int GetHeightSubsample(this ColorSpaces colorSpace)
        {
            if (!colorSpace.IsRealPlanar())
                return 1;
            return colorSpace.HasFlag(ColorSpaces.CS_Sub_Height_1) ? 1 :
                (colorSpace.HasFlag(ColorSpaces.CS_Sub_Height_4) ? 4 : 2);
        }

        public static Size GetSubSample(this ColorSpaces colorSpace)
        {
            return new Size(colorSpace.GetWidthSubsample(), colorSpace.GetHeightSubsample());
        }

        public static string GetName(this ColorSpaces colorSpace)
        {
            return ColorSpaceMap[(int)colorSpace];
        }

        public static bool WithoutSubSample(this ColorSpaces colorSpace)
        {
            return colorSpace.GetSubSample() == NO_SUB_SAMPLE;
        }

        public static int GetBitDepth(this ColorSpaces colorSpace)
        {
            return BitDepths.First(p => colorSpace.HasFlag(p.Key)).Value;
        }

        public static ColorSpaces ChangeBitDepth(this ColorSpaces colorSpace, int depth)
        {
            var en = BitDepths.First(p => p.Value == depth).Key;
            return (colorSpace & ~ColorSpaces.CS_Sample_Bits_Mask) | en;
        }

        public static YUVPlanes[] GetPlanes(this ColorSpaces colorSpace)
        {
            if (colorSpace.HasFlag(ColorSpaces.CS_INTERLEAVED))
                return new[] {default(YUVPlanes)};
            if (colorSpace.HasFlag(ColorSpaces.CS_RGBP))
                return new[] { YUVPlanes.PLANAR_R, YUVPlanes.PLANAR_G, YUVPlanes.PLANAR_B };
            if (colorSpace.HasFlag(ColorSpaces.CS_RGBAP))
                return new[] { YUVPlanes.PLANAR_R, YUVPlanes.PLANAR_G, YUVPlanes.PLANAR_B, YUVPlanes.PLANAR_A };
            return new[] { YUVPlanes.PLANAR_Y, YUVPlanes.PLANAR_U, YUVPlanes.PLANAR_V };
        }

        public static PlaneChannel[] GetPlaneChannels(this ColorSpaces colorSpace) => PlaneChannels[(int)colorSpace];

        public static Bitmap ToBitmap(this VideoFrame frame, PixelFormat pixelFormat, YUVPlanes plane = default(YUVPlanes))
        {
            Bitmap bmp;
            switch (pixelFormat)
            {
                case PixelFormat.Format8bppIndexed:
                    bmp = new Bitmap(frame.GetRowSize(plane), frame.GetHeight(plane), frame.GetPitch(plane), pixelFormat, frame.GetReadPtr(plane));
                    var palette = bmp.Palette;
                    unchecked
                    {
                        for (var i = 0; i < 256; i++)
                            palette.Entries[i] = Color.FromArgb(i, i, i);
                    }
                    bmp.Palette = palette;
                    break;
                case PixelFormat.Format24bppRgb:
                    bmp = new Bitmap(frame.GetRowSize() / 3, frame.GetHeight(), frame.GetPitch(), pixelFormat, frame.GetReadPtr());
                    bmp.RotateFlip(RotateFlipType.Rotate180FlipX);
                    break;
                case PixelFormat.Format32bppPArgb:
                    bmp = new Bitmap(frame.GetRowSize() / 4, frame.GetHeight(), frame.GetPitch(), pixelFormat, frame.GetReadPtr());
                    bmp.RotateFlip(RotateFlipType.Rotate180FlipX);
                    break;
                default:
                    throw new NotImplementedException();
            }
            return bmp;
        }

        public static string Type(this AVSValue value)
        {
            if (value.IsArray()) return "Array";
            if (value.IsBool()) return "Bool";
            if (value.IsClip()) return "Clip";
            if (value.IsFloat()) return "Float";
            if (value.IsInt()) return "Int";
            if (value.IsString()) return "String";
            throw new InvalidOperationException();
        }

        public static void InitArgs(AvisynthFilter filter, AVSValue args)
        {
            var annotatedProperties = GetAnnotatedProperties(filter.GetType());

            var argsDefined = annotatedProperties.Sum(p => p.Item1.PropertyType == typeof(Space) ? 2 : 1);
            if (argsDefined != args.ArraySize() && argsDefined != args.ArraySize() - 1)
                throw new AvisynthException("Instance attributes count not match to declared");

            var filterName = filter.GetType().Assembly
                .GetCustomAttributes(typeof(AvisynthFilterClassAttribute), true)
                .OfType<AvisynthFilterClassAttribute>()
                .FirstOrDefault(p => p.FilterType == filter.GetType())
                ?.FilterName ?? filter.GetType().Name;

            var zeroBased = argsDefined == args.ArraySize();
            var argIndex = zeroBased ? 0 : 1;
            var annotatedPropertiesWithArguments = annotatedProperties.Select(p =>
            {
                AVSValue argument;
                if (p.Item1.PropertyType == typeof(Space))
                {
                    argument = new AVSValue(args[argIndex++], args[argIndex++]);
                    if (!argument[0].Defined() && !argument[1].Defined())
                        argument = new AVSValue();
                }
                else
                {
                    argument = args[argIndex++];
                }

                var property = p.Item1;
                return new
                {
                    Argument = argument,
                    Property = p.Item1,
                    Name = property.Name,
                    QualifiedName = $"{filterName}.{property.Name}",
                    Type = property.PropertyType,
                    Defined = argument.Defined(),
                    Attribute = p.Item2
                };
            }).ToDictionary(p => p.Name, p => p);

            foreach (var tuple in annotatedPropertiesWithArguments.Values)
            {
                if (tuple.Attribute.Required && !tuple.Defined)
                    throw new AvisynthException($"{tuple.QualifiedName} is required but not defined");
                if (!tuple.Defined)
                {
                    if (tuple.Type.IsArray)
                    {
                        var emptyArray = Array.CreateInstance(tuple.Type.GetElementType(), 0);
                        tuple.Property.GetSetMethod(true).Invoke(filter, [emptyArray]);
                    }
                    continue;
                }

                var defValue = tuple.Property.GetValue(filter);
                var value = tuple.Argument.AsObject();

                Rect ReadRect(int i)
                {
                    using var frame = ((Clip) value).GetFrame(i, DynamicEnvironment.Env);
                    return Rect.FromFrame(frame);
                }

                if (tuple.Type == typeof(Rectangle))
                {
                    tuple.Property.GetSetMethod(true).Invoke(filter, [ReadRect(0).ToRectangle()]);
                    continue;
                }

                if (tuple.Type == typeof(RectangleD))
                {
                    tuple.Property.GetSetMethod(true).Invoke(filter, [ReadRect(0).ToRectangleD()]);
                    continue;
                }

                if (tuple.Type == typeof(Space))
                {
                    var defSpace = (Space) defValue;
                    var space = new Space(
                        tuple.Argument[0].AsFloat(tuple.Argument[1].AsFloat(defSpace.X)), 
                        tuple.Argument[1].AsFloat(tuple.Argument[0].AsFloat(defSpace.Y)));
                    ValidateRange(tuple.QualifiedName + "X", tuple.Attribute, space.X);
                    ValidateRange(tuple.QualifiedName + "Y", tuple.Attribute, space.Y);
                    tuple.Property.GetSetMethod(true).Invoke(filter, new object[] { space });
                    continue;
                }

                void InitCollection<T>(Func<int, T> converter)
                {
                    var col = (ICollection<T>) Activator.CreateInstance(tuple.Type);

                    var clip = value as Clip;
                    var numFrames = clip.GetVideoInfo().num_frames;
                    if (numFrames > tuple.Attribute.Max)
                        throw new AvisynthException($"{tuple.QualifiedName} contains {value} values but limit is {tuple.Attribute.Max}");
                    for (var i = 0; i < numFrames; i++)
                        col.Add(converter.Invoke(i));
                    tuple.Property.GetSetMethod(true).Invoke(filter, [col]);
                }

                if (typeof(ICollection<Rectangle>).IsAssignableFrom(tuple.Type))
                {
                    InitCollection(i => ReadRect(i).ToRectangle());
                    continue;
                }

                if (typeof(ICollection<RectangleD>).IsAssignableFrom(tuple.Type))
                {
                    InitCollection(i => ReadRect(i).ToRectangleD());
                    continue;
                }

                ValidateRange(tuple.QualifiedName, tuple.Attribute, value);
                if (filter is OverlayFilter overlayFilter && value is Clip supportClip)
                {
                    overlayFilter.Attach(supportClip);
                    if (typeof(SupportFilter).IsAssignableFrom(tuple.Type))
                    {
                        value = SupportFilter.FindFilter(tuple.Type, supportClip, 0);
                        overlayFilter.Attach(supportClip);
                    }

                    if (typeof(SupportFilter[]).IsAssignableFrom(tuple.Type))
                    {
                        var length = supportClip.GetVideoInfo().num_frames;
                        var filterType = tuple.Type.GetElementType();
                        value = Enumerable.Range(0, length)
                            .Select(n => new { n, Filter = SupportFilter.FindFilter(filterType, supportClip, n) })
                            .Aggregate(Array.CreateInstance(filterType, length), (array, pair) =>
                            {
                                array.SetValue(pair.Filter, pair.n);
                                return array;
                            });
                    }
                }
                if (tuple.Type.IsEnum && value is string)
                    tuple.Attribute.Values = Enum.GetNames(tuple.Type);
                if (tuple.Type.IsEnum && value is int)
                    tuple.Attribute.Values = Enum.GetValues(tuple.Type).Cast<object>().Select(p => Convert.ToInt32(p).ToString()).ToArray();
                if (tuple.Attribute.Values.Any() && tuple.Attribute.Values.Select(p => p.ToLower()).All(p => !p.Equals(value.ToString().ToLower())))
                    throw new AvisynthException($"{tuple.QualifiedName} is equal to '{value}' but allowed values are [{string.Join(", ", tuple.Attribute.Values)}]");
                if (tuple.Property.PropertyType.IsEnum && value is string)
                    value = Enum.Parse(tuple.Type, value.ToString(), true);
                if (tuple.Property.PropertyType.IsEnum && value is int)
                    value = Enum.ToObject(tuple.Type, value);
                tuple.Property.GetSetMethod(true).Invoke(filter, [value]);
            }

            var presets = from tuple in annotatedPropertiesWithArguments.Values
                let property = tuple.Property
                where property.PropertyType.IsEnum
                let value = (Enum)property.GetGetMethod(true).Invoke(filter, [])
                where value != null
                let preset = Presets.Find(value, filter)
                where preset != null
                select preset;
            foreach (var preset in presets)
            {
                foreach (var pair in preset)
                {
                    var tuple = annotatedPropertiesWithArguments[pair.Key];
                    if (!tuple.Defined)
                        tuple.Property.GetSetMethod(true).Invoke(filter, [pair.Value(filter)]);
                }
            }
        }

        private static void ValidateRange(string propertyName, AvsArgumentAttribute attr, object value)
        {
            if (!attr.Min.Equals(double.MinValue) && Convert.ToDouble(value) < attr.Min)
                throw new AvisynthException($"{propertyName} is equal to {value} but must be greater or equal to {attr.Min}");
            if (!attr.Max.Equals(double.MaxValue) && Convert.ToDouble(value) > attr.Max)
                throw new AvisynthException($"{propertyName} is equal to {value} but must be less or equal to {attr.Max}");
        }

        public static (PropertyInfo, AvsArgumentAttribute)[] GetAnnotatedProperties(Type filter)
        {
            return filter.GetProperties(
                    BindingFlags.NonPublic | BindingFlags.Public |
                    BindingFlags.Instance | BindingFlags.GetProperty |
                    BindingFlags.SetProperty)
                .Select(p => (p, (AvsArgumentAttribute) Attribute.GetCustomAttribute(p, typeof(AvsArgumentAttribute))))
                .Where(p => p.Item2 != null).ToArray();
        }

        public static void Dispose(AvisynthFilter filter)
        {
            var props = GetAnnotatedProperties(filter.GetType()).Select(p => p.Item1)
                .Where(p => p.PropertyType == typeof(Clip) || p.PropertyType == typeof(Clip[]))
                .Select(p => p.GetGetMethod(true).Invoke(filter, null))
                .Where(p => p != null);
            foreach (var prop in props)
            {
                switch (prop)
                {
                    case Clip clip:
                        clip.Dispose();
                        break;
                    case Clip[] clips:
                    {
                        foreach (var itemClip in clips)
                            itemClip.Dispose();
                        break;
                    }
                }
            }
        }

        public static Size GetSize(this Clip clip)
        {
            var info = clip.GetVideoInfo();
            return info.GetSize();
        }

        public static Size GetSize(this VideoInfo info)
        {
            return new Size(info.width, info.height);
        }

        public static double GetAspectRatio(this Size size)
        {
            return (double) size.Width / size.Height;
        }

        public static float GetAspectRatio(this SizeF size)
        {
            return size.Width / size.Height;
        }

        public static object AsObject(this AVSValue value)
        {
            if (value.IsArray())
            {
                var length = value.ArraySize();
                if (length == 0)
                    return null;
                var type = value[0].AsObject()?.GetType() ?? typeof(object);
                var arrayType = type.MakeArrayType();
                var array = (Array) Activator.CreateInstance(arrayType, length);
                for (var i = 0; i < length; i++)
                    array.SetValue(value[i].AsObject(), i);
                return array;
            }
            if (!value.Defined())
                return null;
            if (value.IsString())
                return value.AsString();
            if (value.IsClip())
                return value.AsClip();
            if (value.IsInt())
                return value.AsInt();
            if (value.IsFloat())
                return value.AsFloat();
            if (value.IsBool())
                return value.AsBool();
            throw new ArgumentException("Unrecognized AvsValue type");
        }

        public static bool IsLuma(this YUVPlanes plane)
        {
            return plane != YUVPlanes.PLANAR_U && plane != YUVPlanes.PLANAR_V;
        }

        public static bool IsChroma(this YUVPlanes plane)
        {
            return plane == YUVPlanes.PLANAR_U || plane == YUVPlanes.PLANAR_V;
        }

        public static string GetLetter(this YUVPlanes plane)
        {
            return plane.ToString().Last().ToString();
        }

        public static string GetKey(this YUVPlanes plane)
        {
            switch (plane)
            {
                case YUVPlanes.PLANAR_Y: return "y";
                case YUVPlanes.PLANAR_V: return "v";
                case YUVPlanes.PLANAR_U: return "u";
                default: return null;
            }
        }

        public static double GetFraction(this double val)
        {
            return val - Math.Truncate(val);
        }

        public static int GetWarpResampleMode(string resizeFunction)
        {
            var lower = resizeFunction.ToLower();
            if (lower.StartsWith("bilinear"))
                return 1;
            if (lower.StartsWith("point"))
                return 0;
            return 2;
        }
        public static double StdDev(IEnumerable<OverlayInfo> sample)
        {
            var mean = Mean(sample);
            return Math.Sqrt(sample.Sum(p => Math.Pow(p.Diff - mean, 2)));
        }

        public static double Mean(IEnumerable<OverlayInfo> sample)
        {
            return sample.Sum(p => p.Diff) / sample.Count();
        }

        public static bool CheckDev(IEnumerable<OverlayInfo> sample, double maxDiffIncrease, bool abs)
        {
            var mean = Mean(sample);
            return sample.All(p => (abs ? Math.Abs(p.Diff - mean) : p.Diff - mean) <= maxDiffIncrease);
        }

        public static dynamic GetConvertFunction(ColorSpaces colorSpace)
        {
            return colorSpace switch
            {
                ColorSpaces.CS_BGR24 => "ConvertToRGB24",
                ColorSpaces.CS_BGR48 => "ConvertToRGB48",
                var p when p.HasFlag(ColorSpaces.CS_GENERIC_YUV420) => "ConvertToYUV420",
                var p when p.HasFlag(ColorSpaces.CS_GENERIC_YUV422) => "ConvertToYUV422",
                var p when p.HasFlag(ColorSpaces.CS_GENERIC_YUV444) => "ConvertToYUV444",
                _ => throw new ArgumentException("Unsupported color space with specified matrix")
            };
        }

        public static Clip ExtractPlane(this Clip clip, YUVPlanes plane)
        {
            return plane == default
                ? clip
                : clip.Dynamic().Invoke("Extract" + plane.GetLetter());
        }

        public static Space AsSpace(this SizeF size) => new(size.Width, size.Height);
        public static Space AsSpace(this Size size) => size;
        public static Space AsSpace(this PointF point) => point;
        public static Space AsSpace(this Point point) => point;
        public static Space AsSpace(this RectangleF rect) => rect;
        public static Space AsSpace(this Rectangle rect) => rect;

        public static Space Median(this RectangleF rect) => new(rect.X + rect.Width/ 2, rect.Y + rect.Height / 2);

        public static Rectangle Scale(this Rectangle rect, double coef) =>
            new((int) Math.Round(rect.X * coef), 
                (int) Math.Round(rect.Y * coef), 
                (int) Math.Round(rect.Width * coef),
                (int) Math.Round(rect.Height * coef));

        public static Size Eval(this Size size, Func<int, int> eval) => new(eval(size.Width), eval(size.Height));
        public static SizeF Eval(this SizeF size, Func<float, float> eval) => new(eval(size.Width), eval(size.Height));

        public static float GetArea(this SizeF size) => size.Width * size.Height;

        public static int GetArea(this Size size) => size.Width * size.Height;

        public static Rectangle Floor(this RectangleF rect) => Rectangle.FromLTRB(
            (int) Math.Ceiling(Math.Round(rect.Left, FRACTION)),
            (int) Math.Ceiling(Math.Round(rect.Top, FRACTION)),
            (int) Math.Floor(Math.Round(rect.Right, FRACTION)), 
            (int) Math.Floor(Math.Round(rect.Bottom, FRACTION)));

        public static T Also<T>(this T obj, Action<T> action)
        {
            action(obj);
            return obj;
        }

        public static IEnumerable<T> Enumerate<T>(this T obj)
        {
            yield return obj;
        }

        public static Size Fit(this Size size, Size other)
        {
            var ar = size.GetAspectRatio();
            var otherAr = other.GetAspectRatio();
            if (ar > otherAr)
            {
                var height = (int)Math.Round(other.Width / ar);
                return new Size(other.Width, height);
            }
            var width = (int)Math.Round(other.Height * ar);
            return new Size(width, other.Height);
        }

        public static dynamic InitClip(dynamic clip, Size size, int color)
        {
            return InitClip(clip, size.Width, size.Height, color);
        }

        public static dynamic InitClip(dynamic clip, int width, int height, int color)
        {
            return ((Clip)clip).GetVideoInfo().IsRGB()
                ? clip.BlankClip(width: width, height: height, color: color)
                : clip.BlankClip(width: width, height: height, color_yuv: color);
        }

        public static Clip GetBlankClip(Clip clip, bool white)
        {
            return clip.GetVideoInfo().IsRGB()
                ? clip.Dynamic().BlankClip(color: white ? 0xFFFFFF : 0)
                : clip.Dynamic().BlankClip(color_yuv: white ? 0xFF8080 : 0x008080);
        }
    }
}
