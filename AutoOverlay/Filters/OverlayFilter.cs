using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
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

        private static ConcurrentDictionary<string, OverlayFilter> Filters { get; } = new();
        public string FilterId { get; } = Guid.NewGuid().ToString();

        private readonly LinkedList<Clip> attached = new();

        protected OverlayFilter()
        {
            Filters[FilterId] = this;
        }

        public void Attach(Clip clip)
        {
            attached.AddLast(clip);
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
            if (disposing)
            {
                OverlayUtils.Dispose(this);
                topLevel?.Dispose();
                topLevel = null;
                foreach (var clip in attached)
                    clip.Dispose();
            }
            Filters.TryRemove(FilterId, out _);
            base.Dispose(disposing);
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
            Copy(frame, res, GetVideoInfo().pixel_type.GetPlanes());
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
