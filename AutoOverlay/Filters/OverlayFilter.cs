using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using AutoOverlay.Overlay;
using AvsFilterNet;

namespace AutoOverlay
{
    public abstract class OverlayFilter : AvisynthFilter
    {
        public virtual bool Debug { get; protected set; }

        public dynamic DynamicEnv => DynamicEnvironment.Env;
        public ScriptEnvironment StaticEnv => DynamicEnvironment.Env;

        private static ISet<OverlayFilter> Filters { get; } = new HashSet<OverlayFilter>();

        private DynamicEnvironment topLevel;

        protected OverlayFilter()
        {
            Filters.Add(this);
        }

        public sealed override void Initialize(AVSValue args, ScriptEnvironment env)
        {
            try
            {
                using (new DynamicEnvironment(env))
                {

                    OverlayUtils.InitArgs(this, args);
                    Initialize(args);
                    base.Initialize(args, env);
                }
                topLevel = new DynamicEnvironment(env, false);
                AfterInitialize();
                topLevel.Detach();
            }
            catch (Exception ex)
            {
                try
                {
                    DisposeAll();
                }
                finally
                {
                    throw ex;
                }
            }
        }

        protected virtual void Initialize(AVSValue args)
        {
            var vi = new VideoInfo
            {
                width = 640,
                height = 320,
                pixel_type = ColorSpaces.CS_BGR32,
                fps_numerator = 25,
                fps_denominator = 1,
                num_frames = 1
            };
            SetVideoInfo(ref vi);
        }

        protected virtual void AfterInitialize()
        {
        }

        public sealed override VideoFrame GetFrame(int n, ScriptEnvironment env)
        {
            using (new DynamicEnvironment(env))
            {
                try
                {
                    return GetFrame(n);
                }
                catch (Exception ex)
                {
                    try
                    {
                        DisposeAll();
                    }
                    finally
                    {
                        throw ex;
                    }
                }
            }
        }

        protected virtual VideoFrame GetFrame(int n)
        {
            return Debug ? GetSubtitledFrame(ToString()) : NewVideoFrame(StaticEnv);
        }

        protected VideoFrame GetSubtitledFrame(string text)
        {
            var blank = DynamicEnv.BlankClip(width: GetVideoInfo().width, height: GetVideoInfo().height);
            var subtitled = blank.Subtitle(text.Replace("\n", "\\n"), align: 8, lsp: 0, size: 24);
            return subtitled[0];
        }

        protected Clip GetBlankClip(Clip clip, bool white)
        {
            if (clip.GetVideoInfo().pixel_type.HasFlag(ColorSpaces.CS_PLANAR | ColorSpaces.CS_INTERLEAVED))
                return DynamicEnv.BlankClip(clip, color_yuv: white ? 0xFF0000 : 0x000000);
            if (clip.GetVideoInfo().pixel_type.HasFlag(ColorSpaces.CS_PLANAR))
                return DynamicEnv.BlankClip(clip, color_yuv: white ? 0xFF8080 : 0x008080);
            return DynamicEnv.BlankClip(clip, color: white ? 0xFFFFFF : 0);
        }
        protected dynamic InitClip(dynamic clip, int width, int height, int color, string pixelType = null)
        {
            var rgb = pixelType?.StartsWith("RGB") ?? ((Clip) clip).GetVideoInfo().IsRGB();
            return rgb ?
                clip.BlankClip(width: width, height: height, color: color, pixel_type: pixelType) :
                clip.BlankClip(width: width, height: height, color_yuv: color, pixel_type: pixelType);
        }

        public dynamic ResizeRotate(
            Clip clip, 
            string resizeFunc, string rotateFunc, 
            int width, int height, int angle = 0,
            RectangleD crop = default)
        {
            if (clip == null || crop == RectangleD.Empty && width == clip.GetVideoInfo().width && height == clip.GetVideoInfo().height)
                return clip.Dynamic();

            var intCrop = Rectangle.FromLTRB(
                (int) Math.Floor(crop.Left),
                (int) Math.Floor(crop.Top),
                (int) Math.Floor(crop.Right),
                (int) Math.Floor(crop.Bottom)
            );
            if (!intCrop.IsEmpty)
            {
                clip = DynamicEnv.Crop(clip, intCrop.Left, intCrop.Top, -intCrop.Right, -intCrop.Bottom);
                crop = RectangleD.FromLTRB(
                    crop.Left - intCrop.Left,
                    crop.Top - intCrop.Top,
                    crop.Right - intCrop.Right,
                    crop.Bottom - intCrop.Bottom
                );
            }

            dynamic resized;
            if (crop == RectangleD.Empty)
                resized = clip.Dynamic().Invoke(resizeFunc, width, height);
            else resized = clip.Dynamic().Invoke(resizeFunc, width, height,
                src_left: crop.Left, src_top: crop.Top, 
                src_width: -crop.Right, src_height: -crop.Bottom);
            if (angle == 0)
                return resized;
            return resized.Invoke(rotateFunc, angle / 100.0);
        }

        protected void Log(Func<string> supplier)
        {
#if DEBUG
            if (Debug)
                System.Diagnostics.Debug.WriteLine(supplier());
#endif
        }

        protected void Log(string format, params object[] args)
        {
#if DEBUG
            if (args.Length == 0)
                System.Diagnostics.Debug.WriteLine(format);
            else
                System.Diagnostics.Debug.WriteLine(format, args);
#endif
        }

        protected override void Dispose(bool A_0)
        {
            Filters.Remove(this);
            OverlayUtils.Dispose(this);
            base.Dispose(A_0);
            topLevel.Dispose();
        }

        private static void DisposeAll()
        {
            foreach (var filter in Filters.ToArray())
                filter.Dispose();
        }

        protected void Copy(VideoFrame frame, VideoFrame res, YUVPlanes[] planes)
        {
            using (frame)
            {
                Parallel.ForEach(planes, plane => OverlayUtils.CopyPlane(frame, res, plane));
            }
        }

        protected VideoFrame Copy(VideoFrame frame)
        {
            var res = NewVideoFrame(StaticEnv);
            Copy(frame, res, OverlayUtils.GetPlanes(GetVideoInfo().pixel_type));
            return res;
        }
    }
}
