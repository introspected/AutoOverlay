﻿using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AutoOverlay.AviSynth;
using AutoOverlay.Overlay;
using AvsFilterNet;

namespace AutoOverlay
{
    public abstract class OverlayFilter : AvisynthFilter
    {
        public virtual bool Debug { get; protected set; }

        public dynamic DynamicEnv => DynamicEnvironment.Env;
        public ScriptEnvironment StaticEnv => DynamicEnvironment.Env ?? topLevel;
        private DynamicEnvironment topLevel;

        protected static ConcurrentDictionary<string, OverlayFilter> Filters { get; } = new();
        public string FilterId { get; } = Guid.NewGuid().ToString();
        private long references = 1;

        protected OverlayFilter()
        {
            Filters[FilterId] = this;
        }

        public OverlayFilter Attach()
        {
            Interlocked.Increment(ref references);
            return this;
        }

        public static T FindFilter<T>(string filterId) where T : OverlayFilter => Filters[filterId] as T;

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
                catch
                {
                    // ignored
                }
                finally
                {
                    if (ex is SEHException)
                        throw new AvisynthException("Runtime function call error: " + DynamicEnvironment.LastError);
                    DynamicEnvironment.LastError = ex.Message;
                    throw new AvisynthException(ex.Message);
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
                        if (ex is SEHException)
                            throw new AvisynthException("Runtime function call error: " + DynamicEnvironment.LastError);
                        DynamicEnvironment.LastError = ex.Message;
                        throw new AvisynthException(ex.Message ?? "");
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

        public Clip GetBlankClip(Clip clip, bool white)
        {
            return clip.GetVideoInfo().IsRGB() ? 
                (Clip) DynamicEnv.BlankClip(clip, color: white ? 0xFFFFFF : 0) : 
                (Clip) DynamicEnv.BlankClip(clip, color_yuv: white ? 0xFF8080 : 0x008080);
        }

        protected dynamic InitClip(dynamic clip, int width, int height, int color)
        {
            return ((Clip) clip).GetVideoInfo().IsRGB()
                ? clip.BlankClip(width: width, height: height, color: color)
                : clip.BlankClip(width: width, height: height, color_yuv: color);
        }

        public dynamic ResizeRotate(Clip clip, string resizeFunc, string rotateFunc, OverlayData data)
        {
            return ResizeRotate(clip, resizeFunc, rotateFunc,
                data.Overlay.Width, data.Overlay.Height,
                data.OverlayAngle, data.OverlayCrop, data.OverlayWarp);
        }

        public dynamic ResizeRotate(
            Clip clip,
            string resizeFunc, string rotateFunc,
            int width, int height, double angle = 0,
            RectangleD crop = default, Warp warp = default)
        {
            if (clip == null)
                return null;
            var dynamic = clip.Dynamic();
            if (warp != null && !warp.IsEmpty)
                dynamic = dynamic.Warp(warp.ToArray(), relative: true,
                    resample: OverlayUtils.GetWarpResampleMode(resizeFunc));

            var vi = clip.GetVideoInfo();

            var intCrop = Rectangle.FromLTRB(
                (int) Math.Floor(crop.Left),
                (int) Math.Floor(crop.Top),
                (int) Math.Floor(crop.Right),
                (int) Math.Floor(crop.Bottom)
            );
            if (!intCrop.IsEmpty)
            {
                dynamic = dynamic.Crop(intCrop.Left, intCrop.Top, -intCrop.Right, -intCrop.Bottom);
                crop = RectangleD.FromLTRB(
                    crop.Left - intCrop.Left,
                    crop.Top - intCrop.Top,
                    crop.Right - intCrop.Right,
                    crop.Bottom - intCrop.Bottom
                );
            }

            if (crop.IsEmpty)
            {
                if (width != vi.width - intCrop.Left - intCrop.Right ||
                    height != vi.height - intCrop.Top - intCrop.Bottom)
                {
                    dynamic = dynamic.Invoke(resizeFunc, width, height);
                }
            }
            else
            {
                dynamic = dynamic.Invoke(resizeFunc, width, height,
                    src_left: crop.Left, src_top: crop.Top,
                    src_width: -crop.Right, src_height: -crop.Bottom);
            }
            //TODO rotate first
            return angle == 0 ? dynamic : dynamic.Invoke(rotateFunc, angle);
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
            if (!Debug) return;
            if (args.Length == 0)
                System.Diagnostics.Debug.WriteLine(format);
            else
                System.Diagnostics.Debug.WriteLine(format, args);
#endif
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Decrement(ref references) == 0 && Filters.TryRemove(FilterId, out _))
            {
                base.Dispose(disposing);
                OverlayUtils.Dispose(this);
                topLevel?.Dispose();
                topLevel = null;
            }
        }

        private static void DisposeAll()
        {
            foreach (var filter in Filters.Values.ToArray())
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

        protected bool Equals(OverlayFilter other)
        {
            return FilterId == other.FilterId;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((OverlayFilter)obj);
        }

        public override int GetHashCode()
        {
            return (FilterId != null ? FilterId.GetHashCode() : 0);
        }
    }
}
