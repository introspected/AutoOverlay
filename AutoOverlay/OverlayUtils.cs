using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
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

        public static int GetWidthSubsample(this ColorSpaces colorSpace)
        {
            return colorSpace.HasFlag(ColorSpaces.CS_Sub_Width_1) ? 1 :
                (colorSpace.HasFlag(ColorSpaces.CS_Sub_Width_4) ? 4 : 2);
        }

        public static int GetHeightSubsample(this ColorSpaces colorSpace)
        {
            return colorSpace.HasFlag(ColorSpaces.CS_Sub_Height_1) ? 1 :
                (colorSpace.HasFlag(ColorSpaces.CS_Sub_Height_4) ? 4 : 2);
        }

        private static readonly Dictionary<ColorSpaces, int> bitDepths = new Dictionary<ColorSpaces, int>
        {
            {ColorSpaces.CS_Sample_Bits_14, 14},
            {ColorSpaces.CS_Sample_Bits_12, 12},
            {ColorSpaces.CS_Sample_Bits_10, 10},
            {ColorSpaces.CS_Sample_Bits_16, 16},
            {ColorSpaces.CS_Sample_Bits_32, 32},
            {ColorSpaces.CS_Sample_Bits_8, 8}
        };

        public static int GetBitDepth(this ColorSpaces colorSpace)
        {
            return bitDepths.First(p => colorSpace.HasFlag(p.Key)).Value;
        }

        public static ColorSpaces ChangeBitDepth(this ColorSpaces colorSpace, int depth)
        {
            var en = bitDepths.First(p => p.Value == depth).Key;
            return (colorSpace & ~ColorSpaces.CS_Sample_Bits_Mask) | en;
        }

        public static YUVPlanes[] GetPlanes(ColorSpaces colorSpace)
        {
            if (colorSpace.HasFlag(ColorSpaces.CS_INTERLEAVED))
                return new[] {default(YUVPlanes)};
            return new[] { YUVPlanes.PLANAR_Y, YUVPlanes.PLANAR_U, YUVPlanes.PLANAR_V };
        }

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
                    bmp = new Bitmap(frame.GetRowSize()/3, frame.GetHeight(), frame.GetPitch(), pixelFormat, frame.GetReadPtr());
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
            var annotatedProperties = GetAnnotatedProperties(filter);

            if (annotatedProperties.Length != args.ArraySize() && annotatedProperties.Length != args.ArraySize() - 1)
                throw new AvisynthException("Instance attributes count not match to declared");
            var zeroBased = annotatedProperties.Length == args.ArraySize();

            var annotatedPropertiesWithArguments = annotatedProperties.Select((p, i) => new
            {
                Argument = args[zeroBased ? i : ++i],
                Property = p.Item1,
                Attribute = p.Item2
            });

            var filterName = filter.GetType().Assembly
                                 .GetCustomAttributes(typeof(AvisynthFilterClassAttribute), true)
                                 .OfType<AvisynthFilterClassAttribute>()
                                 .FirstOrDefault(p => p.FilterType == filter.GetType())
                                 ?.FilterName ?? filter.GetType().Name;

            foreach (var tuple in annotatedPropertiesWithArguments)
            {
                var propertyName = $"{filterName}.{tuple.Property.Name}";
                var isDefined = tuple.Argument.Defined();
                if (tuple.Attribute.Required && !isDefined)
                    throw new AvisynthException($"{propertyName} is required but not defined");
                if (!isDefined)
                    continue;
                var value = tuple.Argument.AsObject();
                if (!tuple.Attribute.Min.Equals(double.MinValue) && Convert.ToDouble(value) < tuple.Attribute.Min)
                    throw new AvisynthException($"{propertyName} is equal to {value} but must be greater or equal to {tuple.Attribute.Min}");
                if (!tuple.Attribute.Max.Equals(double.MaxValue) && Convert.ToDouble(value) > tuple.Attribute.Max)
                    throw new AvisynthException($"{propertyName} is equal to {value} but must be less or equal to {tuple.Attribute.Max}");
                if (tuple.Property.PropertyType.IsEnum && value is string)
                    tuple.Attribute.Values = Enum.GetNames(tuple.Property.PropertyType);
                if (tuple.Property.PropertyType.IsEnum && value is int)
                    tuple.Attribute.Values = Enum.GetValues(tuple.Property.PropertyType).Cast<object>().Select(p => Convert.ToInt32(p).ToString()).ToArray();
                if (tuple.Attribute.Values.Any() && tuple.Attribute.Values.Select(p => p.ToLower()).All(p => !p.Equals(value.ToString())))
                    throw new AvisynthException($"{propertyName} is equal to '{value}' but allowed values are [{string.Join(", ", tuple.Attribute.Values)}]");
                if (tuple.Property.PropertyType.IsEnum && value is string)
                    value = Enum.Parse(tuple.Property.PropertyType, value.ToString(), true);
                if (tuple.Property.PropertyType.IsEnum && value is int)
                    value = Enum.ToObject(tuple.Property.PropertyType, value);
                tuple.Property.GetSetMethod(true).Invoke(filter, new[] {value});
            }
        }

        public static Tuple<PropertyInfo, AvsArgumentAttribute>[] GetAnnotatedProperties(AvisynthFilter filter)
        {
            return filter.GetType().GetProperties(
                    BindingFlags.NonPublic | BindingFlags.Public |
                    BindingFlags.Instance | BindingFlags.GetProperty |
                    BindingFlags.SetProperty)
                .Select(p =>
                    Tuple.Create(p,
                        (AvsArgumentAttribute) Attribute.GetCustomAttribute(p, typeof(AvsArgumentAttribute))))
                .Where(p => p.Item2 != null).ToArray();
        }

        public static void Dispose(AvisynthFilter filter)
        {
            GetAnnotatedProperties(filter).Select(p => p.Item1).Where(p => p.PropertyType == typeof(Clip))
                .Select(p => p.GetGetMethod(true).Invoke(filter, null)).OfType<Clip>().ToList()
                .ForEach(p => p.Dispose());
        }

        public static object AsObject(this AVSValue value)
        {
            if (value.IsArray())
            {
                var length = value.ArraySize();
                if (length == 0)
                    return null;
                var type = value[0].AsObject().GetType();
                var arrayType = type.MakeArrayType();
                var array = (Array) Activator.CreateInstance(arrayType, new object[length]);
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
    }
}
