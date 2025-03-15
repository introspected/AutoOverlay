using AutoOverlay.AviSynth;
using AvsFilterNet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;
using AutoOverlay.Core;

namespace AutoOverlay
{
    public static class AvsUtils
    {
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
                _ => []
            });

        private static readonly Dictionary<int, PlaneChannel[]> PlanesOnly = PlaneChannels
            .ToDictionary(p => p.Key, p => p.Value.Where(v => v.ChannelOffset == 0).ToArray());

        public static ICollection<string> Matrices => new LinkedHashSet<string>
        {
            "Rec601",
            "Rec709",
            "Rec2020",
            "PC.601",
            "PC.709",
            "PC.2020",
            "Average",
        };

        public static PlaneChannel[] Interleaved(int channelCount, int depth, params YUVPlanes[] effectivePlanes) => Enumerable.Range(0, channelCount)
            .Select(i => new PlaneChannel(default, effectivePlanes[i], i, channelCount, depth)).ToArray();

        public static PlaneChannel[] Planar(int depth, params YUVPlanes[] planes) =>
            planes.Select(p => new PlaneChannel(p, p, 0, 1, depth)).ToArray();

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
            return colorSpace.GetSubSample() == OverlayConst.NO_SUB_SAMPLE;
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
                return [default];
            if (colorSpace.HasFlag(ColorSpaces.CS_RGBP))
                return [YUVPlanes.PLANAR_R, YUVPlanes.PLANAR_G, YUVPlanes.PLANAR_B];
            if (colorSpace.HasFlag(ColorSpaces.CS_RGBAP))
                return [YUVPlanes.PLANAR_R, YUVPlanes.PLANAR_G, YUVPlanes.PLANAR_B, YUVPlanes.PLANAR_A];
            return [YUVPlanes.PLANAR_Y, YUVPlanes.PLANAR_U, YUVPlanes.PLANAR_V];
        }

        public static PlaneChannel[] GetPlaneChannels(this ColorSpaces colorSpace, string channels)
        {
            var planeChannels = GetPlaneChannels(colorSpace);
            if (channels == null)
                return planeChannels;
            var filter = channels.ToLower();
            return planeChannels.Where(p => filter.Contains(p.EffectivePlane.GetKey())).ToArray();
        }

        public static PlaneChannel[] GetPlaneChannels(this ColorSpaces colorSpace, YUVPlanes yAlter = default)
        {
            var planeChannels = PlaneChannels[(int)colorSpace];
            var plane = planeChannels[0];
            if (yAlter != default && planeChannels.Length == 1 && plane.EffectivePlane == YUVPlanes.PLANAR_Y)
                return [new PlaneChannel(plane.Plane, yAlter, plane.ChannelOffset, plane.PixelSize, plane.Depth)];
            return planeChannels;
        }

        public static PlaneChannel[] GetPlanesOnly(this ColorSpaces colorSpace) => PlanesOnly[(int)colorSpace];

        public static Bitmap ToBitmap(this VideoFrame frame, PixelFormat pixelFormat, YUVPlanes plane = default)
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
                case PixelFormat.Format32bppRgb:
                    bmp = new Bitmap(frame.GetRowSize() / 4, frame.GetHeight(), frame.GetPitch(), pixelFormat, frame.GetReadPtr());
                    bmp.RotateFlip(RotateFlipType.Rotate180FlipX);
                    break;
                default:
                    throw new NotImplementedException();
            }
            return bmp;
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
            if (val is DynamicEnvironment environment)
                val = environment.Clip;
            if (val is Clip clip)
            {
                var cached = DynamicEnvironment.FindClip(clip);
                if (cached != null)
                    return cached;
            }
            if (val is Enum)
                val = val.ToString();

            if (val is not string && val is IEnumerable array)
            {
                return new AVSValue(array.OfType<object>().Select(ToAvsValue).ToArray());
            }
            var ctor = typeof(AVSValue).GetConstructor([val.GetType()]);
            if (ctor == null)
                throw new AvisynthException($"Wrong type '{val.GetType()}' for {nameof(AVSValue)} instantiation");
            return (AVSValue)ctor.Invoke([val]);
        }

        public static dynamic Dynamic(this Clip clip) => clip == null ? null : new DynamicEnvironment(clip);

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
            DotNetUtils.MemSet(frame.GetWritePtr(YUVPlanes.PLANAR_U), 128, frame.GetHeight(YUVPlanes.PLANAR_U) * frame.GetPitch(YUVPlanes.PLANAR_U));
            DotNetUtils.MemSet(frame.GetWritePtr(YUVPlanes.PLANAR_V), 128, frame.GetHeight(YUVPlanes.PLANAR_V) * frame.GetPitch(YUVPlanes.PLANAR_V));
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

        public static object AsObject(this AVSValue value, Type expectedType = null)
        {
            if (value.IsArray())
            {
                var length = value.ArraySize();
                if (length == 0)
                    return null;
                var raw = new object[length];
                var elementType = expectedType is not { IsArray: true } ? null : expectedType.GetElementType();
                for (var i = 0; i < length; i++)
                {
                    var item = value[i].AsObject(elementType);
                    if (elementType != null)
                        item = Convert.ChangeType(item, elementType);
                    raw[i] = item;
                    var type = item?.GetType();
                    if (type != null)
                    {
                        if (elementType == null)
                            elementType = type;
                        else if (!elementType.IsAssignableFrom(type))
                            elementType = typeof(object);
                    }
                }
                if (elementType == null || elementType == typeof(object))
                    return raw;
                var arrayType = elementType.MakeArrayType();
                var array = (Array)Activator.CreateInstance(arrayType, length);
                for (var i = 0; i < raw.Length; i++)
                    array.SetValue(raw[i], i);
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

        public static bool IsRgb(this YUVPlanes plane)
        {
            return plane is YUVPlanes.PLANAR_R or YUVPlanes.PLANAR_G or YUVPlanes.PLANAR_B;
        }

        public static bool IsLuma(this YUVPlanes plane)
        {
            return plane != YUVPlanes.PLANAR_U && plane != YUVPlanes.PLANAR_V;
        }

        public static bool IsChroma(this YUVPlanes plane)
        {
            return plane is YUVPlanes.PLANAR_U or YUVPlanes.PLANAR_V;
        }

        public static string GetLetter(this YUVPlanes plane)
        {
            return plane.ToString().Last().ToString();
        }

        public static YUVPlanes GetPlane(this string plane) => plane?.ToLower() switch
        {
            "y" => YUVPlanes.PLANAR_Y,
            "u" => YUVPlanes.PLANAR_U,
            "v" => YUVPlanes.PLANAR_V,
            "r" => YUVPlanes.PLANAR_R,
            "g" => YUVPlanes.PLANAR_G,
            "b" => YUVPlanes.PLANAR_B,
            _ => default
        };

        public static string GetKey(this YUVPlanes plane) => plane switch
        {
            YUVPlanes.PLANAR_Y => "y",
            YUVPlanes.PLANAR_V => "v",
            YUVPlanes.PLANAR_U => "u",
            YUVPlanes.PLANAR_R => "r",
            YUVPlanes.PLANAR_G => "g",
            YUVPlanes.PLANAR_B => "b",
            _ => null
        };

        public static dynamic GetConvertFunction(this ColorSpaces colorSpace) => colorSpace switch
        {
            ColorSpaces.CS_BGR24 => "ConvertToRGB24",
            ColorSpaces.CS_BGR32 => "ConvertToRGB32",
            ColorSpaces.CS_BGR48 => "ConvertToRGB48",
            ColorSpaces.CS_BGR64 => "ConvertToRGB64",
            _ when colorSpace.HasFlag(ColorSpaces.CS_GENERIC_RGBP) => "ConvertToPlanarRGB",
            _ when colorSpace.HasFlag(ColorSpaces.CS_GENERIC_RGBAP) => "ConvertToPlanarRGBA",
            _ when colorSpace.HasFlag(ColorSpaces.CS_GENERIC_YUV444) => "ConvertToYUV444",
            _ when colorSpace.HasFlag(ColorSpaces.CS_GENERIC_YUV422) => "ConvertToYUV422",
            _ when colorSpace.HasFlag(ColorSpaces.CS_GENERIC_YUV420) => "ConvertToYUV420",
            _ when colorSpace.HasFlag(ColorSpaces.CS_GENERIC_Y) => "ConvertToY",
            _ => throw new ArgumentException("Unsupported color space with specified matrix")
        };

        public static Clip ExtractPlane(this Clip clip, YUVPlanes plane) => plane == default
            ? clip
            : clip.Dynamic().Invoke("Extract" + plane.GetLetter());

        public static dynamic InitClip(dynamic clip, Size size, int color) => InitClip(clip, size.Width, size.Height, color);

        public static dynamic InitClip(dynamic clip, int width, int height, int color)
        {
            return ((Clip)clip).GetVideoInfo().IsRGB()
                ? clip.BlankClip(width: width, height: height, color: color)
                : clip.BlankClip(width: width, height: height, color_yuv: color);
        }

        public static dynamic GetBlankClip(Clip clip, bool white) => GetSolidClip(clip, white ? 0xFF : 0x00);

        public static dynamic GetSolidClip(Clip clip, double opacity) => GetSolidClip(clip, (int)Math.Round(0xFF * opacity));

        public static dynamic GetSolidClip(Clip clip, int level) => clip.GetVideoInfo().IsRGB()
            ? clip.Dynamic().BlankClip(color: (level << 16) + (level << 8) + level)
            : clip.Dynamic().BlankClip(color_yuv: (level << 16) + 0x8080);

        public static Clip ROI(this Clip clip, Rectangle roi) => clip.Dynamic().Crop(roi.X, roi.Y, roi.Width, roi.Height);

        public static void CopyTo(this VideoFrame src, VideoFrame target, YUVPlanes[] planes)
        {
            ScriptEnvironment env = DynamicEnvironment.Env;
            Parallel.ForEach(planes, plane =>
            {
                env.BitBlt(target.GetWritePtr(plane), target.GetPitch(plane),
                    src.GetReadPtr(plane), src.GetPitch(plane),
                    target.GetRowSize(plane), target.GetHeight(plane));
            });
        }

        public static string FunctionCoalesce(this ScriptEnvironment env, params string[] functions)
        {
            foreach (var function in functions)
                if (env.FunctionExists(function))
                    return function;
            return null;
        }

        public static ColorSpaces ParseColorSpace(this string colorSpace)
        {
            colorSpace = colorSpace.ToUpper();
            if (Enum.TryParse("CS_" + colorSpace, true, out ColorSpaces en)
                || Enum.TryParse("CS_" + colorSpace.Replace("RGB", "BGR"), true, out en))
                return en;
            return colorSpace switch
            {
                "YUV420P8" => ColorSpaces.CS_YV12,
                "YUV422P8" => ColorSpaces.CS_YV16,
                "YUV444P8" => ColorSpaces.CS_YV24,
                _ => throw new AvisynthException("Unsupported color space: " + colorSpace)
            };
        }

        public static ColorSpaces VPlaneFirst(this ColorSpaces pixelType)
        {
            if (pixelType.HasFlag(ColorSpaces.CS_UPlaneFirst))
                return (pixelType ^ ColorSpaces.CS_UPlaneFirst) | ColorSpaces.CS_VPlaneFirst;
            return pixelType;
        }

        public static bool IsHdr(this ColorSpaces pixelType) => (int)(pixelType & ColorSpaces.CS_Sample_Bits_Mask) > 1;

        public static bool IsRgb(this ColorSpaces pixelType) => pixelType.HasFlag(ColorSpaces.CS_BGR);
    }
}
