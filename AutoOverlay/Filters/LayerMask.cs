using AutoOverlay;
using AutoOverlay.Filters;
using AvsFilterNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using AutoOverlay.AviSynth;

[assembly: AvisynthFilterClass(typeof(LayerMask),
    nameof(LayerMask),
    "c[LayerIndex]i[CanvasWidth]i[CanvasHeight]i[Gradient]i[Noise]b[GradientEdge]s[Opacity]f[Layers]c[Seed]i[ExcludedLayers]i*",
    OverlayConst.DEFAULT_MT_MODE)]
namespace AutoOverlay.Filters
{
    public class LayerMask : OverlayFilter
    {
        [AvsArgument(Min = 0)]
        public int LayerIndex { get; set; }

        [AvsArgument(Min = 1)]
        public int CanvasWidth { get; set; }

        [AvsArgument(Min = 1)]
        public int CanvasHeight { get; set; }

        [AvsArgument(Min = 0)]
        public int Gradient { get; set; }

        [AvsArgument(Min = 0)]
        public bool Noise { get; set; }

        [AvsArgument]
        public EdgeGradient GradientEdge { get; set; }

        [AvsArgument]
        public double Opacity { get; set; }

        [AvsArgument(LTRB = false)]
        public List<Rectangle> Layers { get; set; }

        [AvsArgument]
        public int Seed { get; set; }

        [AvsArgument]
        public int[] ExcludedLayers { get; set; }

        private Rectangle currentLayer;
        private dynamic white;
        private PlaneChannel planeChannel;
        private Rectangle canvas;
        private Rectangle union;

        protected override void Initialize(AVSValue args)
        {
            var vi = Child.GetVideoInfo();
            SetVideoInfo(ref vi);
            planeChannel = vi.pixel_type.GetPlaneChannels()[0];
            union = Layers.Aggregate(Rectangle.Union);
            if (CanvasWidth == 0)
                CanvasWidth = union.Width;
            if (CanvasHeight == 0)
                CanvasHeight = union.Height;
            canvas = new Rectangle(0, 0, CanvasWidth, CanvasHeight);
        }

        protected override void AfterInitialize()
        {
            currentLayer = Layers[LayerIndex];
            white = AvsUtils.GetBlankClip(Child, true);
        }

        protected override VideoFrame GetFrame(int n)
        {
            VideoFrame frame = white[0];
            StaticEnv.MakeWritable(frame);
            var framePlane = new FramePlane(planeChannel, frame, false);

            Action RunIf(bool predicate, Action action, Action otherwise = null) => predicate ? action : otherwise ?? (() => { });

            // 1. Outer bounds
            if (GradientEdge != EdgeGradient.NONE)
            {
                var edges = GradientEdge == EdgeGradient.INSIDE ? union : canvas;

                // Edges
                Parallel.Invoke(
                    RunIf(currentLayer.Left != edges.Left, () => framePlane.TakeLeft(Gradient).FillGradient(0x00, 0xFF, 0xFF, 0x00)),
                    RunIf(currentLayer.Right != edges.Right, () => framePlane.TakeRight(Gradient).FillGradient(0xFF, 0x00, 0x00, 0xFF)),
                    RunIf(currentLayer.Top != edges.Top, () => framePlane.TakeTop(Gradient).FillGradient(0x00, 0x00, 0xFF, 0xFF)),
                    RunIf(currentLayer.Bottom != edges.Bottom, () => framePlane.TakeBottom(Gradient).FillGradient(0xFF, 0xFF, 0x00, 0x00))
                );

                // Corners
                Parallel.Invoke(
                    RunIf(currentLayer.Left != edges.Left && currentLayer.Top != edges.Top,
                        () => framePlane.TakeLeft(Gradient).TakeTop(Gradient).FillGradient(0x00, 0x00, 0xFF, 0x00)),
                    RunIf(currentLayer.Right != edges.Right && currentLayer.Top != edges.Top,
                        () => framePlane.TakeRight(Gradient).TakeTop(Gradient).FillGradient(0x00, 0x00, 0x00, 0xFF)),
                    RunIf(currentLayer.Right != edges.Right && currentLayer.Bottom != edges.Bottom,
                        () => framePlane.TakeRight(Gradient).TakeBottom(Gradient).FillGradient(0xFF, 0x00, 0x00, 0x00)),
                    RunIf(currentLayer.Left != edges.Left && currentLayer.Bottom != edges.Bottom,
                        () => framePlane.TakeLeft(Gradient).TakeBottom(Gradient).FillGradient(0x00, 0xFF, 0x00, 0x00))
                );
            }

            // 2. Layers
            foreach (var layer in Layers.Take(LayerIndex).Where((_, i) => !ExcludedLayers.Contains(i)))
            {
                using VideoFrame layerFrame = white[0];
                StaticEnv.MakeWritable(layerFrame);

                var intersection = Rectangle.Intersect(currentLayer, layer);
                var roi = intersection with { X = intersection.X - currentLayer.X, Y = intersection.Y - currentLayer.Y };
                if (intersection.IsEmpty) continue;

                var interFrame = new FramePlane(planeChannel, layerFrame, false, roi, false);

                var opacityColor = (byte)(0xFF * Opacity);
                var nonEdges = GradientEdge == EdgeGradient.NONE;
                var cornerColor = nonEdges ? 0x80 : 0x00;
                var cornerNoise = Noise && nonEdges;

                // 1. Fill intersection area with solid color
                interFrame.Fill(opacityColor);

                // 2. Border gradient if transparent
                // Intersection edges
                if (Noise)
                {
                    Action left = () => interFrame.TakeLeft(Gradient).FillNoise(1, 0, 0, 1, currentLayer.Left > layer.Left ? 0x00 : 0xFF, Seed << 0),
                        top = () => interFrame.TakeTop(Gradient).FillNoise(1, 1, 0, 0, currentLayer.Top > layer.Top ? 0 : 0xFF, Seed << 2),
                        right = () => interFrame.TakeRight(Gradient).FillNoise(0, 1, 1, 0, currentLayer.Right < layer.Right ? 0x00 : 0xFF, Seed << 1),
                        bottom = () => interFrame.TakeBottom(Gradient).FillNoise(0, 0, 1, 1, currentLayer.Bottom < layer.Bottom ? 0 : 0xFF, Seed << 3);

                    if (Opacity is 0 or 1)
                    {
                        Parallel.Invoke(left, top, right, bottom);
                        FillCorners(0, 1, Opacity, 0.5, p => p / 2, (a, b) => a - b,
                            (plane, tl, tr, br, bl, index) => plane.Also(p => p.Fill(0))
                                .RotateNoise(tl, tr, br, bl, 0xFF, index, Seed << index << LayerIndex));
                    }
                    else
                    {
                        Parallel.Invoke(left, right);
                        Parallel.Invoke(top, bottom);
                    }

                    //FillCorners(1, 0, 1 - Opacity, 0.5, p => 0.5 + (1 - p)/2, (a, b) => 1 - b,
                    //    (plane, tl, tr, br, bl, index) => plane.Also(p => p.Fill(0xFF)).RotateNoise(tl, tr, br, bl, 0x00, index, Seed << index << LayerIndex));
                }
                else
                {
                    Parallel.Invoke(
                        FillEdge(0, interFrame.TakeLeft(Gradient), currentLayer.Left > layer.Left),
                        FillEdge(1, interFrame.TakeTop(Gradient), currentLayer.Top > layer.Top),
                        FillEdge(2, interFrame.TakeRight(Gradient), currentLayer.Right < layer.Right),
                        FillEdge(3, interFrame.TakeBottom(Gradient), currentLayer.Bottom < layer.Bottom));

                    Action FillEdge(int index, FramePlane area, bool predicate) => RunIf(predicate,
                        () => area.FillGradient(0x00, opacityColor, opacityColor, 0x00, index),
                        () => area.FillGradient(0xFF, opacityColor, opacityColor, 0xFF, index));

                    FillCorners(0x00, 0xFF, opacityColor, cornerColor, 
                        p => (int)Math.Round(cornerColor * p), (a, b) => a - b, 
                        (plane, tl, tr, br, bl, index) => plane.FillGradient(tl, tr, br, bl, index));
                }

                void FillCorners<T>(T min, T max, T opacity, T edge,
                    Func<double, T> calc, Func<T, T, T> subtract,
                    Action<FramePlane, T, T, T, T, int> fill) => Parallel.Invoke(
                    () => FillCorner(0,
                        interFrame.TakeLeft(Gradient).TakeTop(Gradient),
                        () => currentLayer.Left > layer.Left,
                        () => currentLayer.Top > layer.Top,
                        min, max, opacity, edge, calc, subtract, fill),
                    () => FillCorner(1,
                        interFrame.TakeRight(Gradient).TakeTop(Gradient),
                        () => currentLayer.Top > layer.Top,
                        () => currentLayer.Right < layer.Right,
                        min, max, opacity, edge, calc, subtract, fill),
                    () => FillCorner(2,
                        interFrame.TakeRight(Gradient).TakeBottom(Gradient),
                        () => currentLayer.Right < layer.Right,
                        () => currentLayer.Bottom < layer.Bottom,
                        min, max, opacity, edge, calc, subtract, fill),
                    () => FillCorner(3,
                        interFrame.TakeLeft(Gradient).TakeBottom(Gradient),
                        () => currentLayer.Bottom < layer.Bottom,
                        () => currentLayer.Left > layer.Left,
                        min, max, opacity, edge, calc, subtract, fill));

                void FillCorner<T>(int index, FramePlane corner, 
                    Func<bool> condition1, Func<bool> condition2, 
                    T min, T max, T opacity, T edge, 
                    Func<double, T> calc, Func<T,T,T> subtract,
                    Action<FramePlane, T, T, T, T, int> fill)
                {
                    Action<FramePlane> Fill(T min, T max, T opacity, T edge) => area => fill(area, min, max, opacity, edge, index);

                    if (condition1() && condition2())
                        corner.Also(Fill(min, min, opacity, min));
                    else if (condition1())
                    {
                        if (!nonEdges)
                        {
                            corner.Also(Fill(edge, max, opacity, min));
                            return;
                        }
                        corner.Crop(0, 0, Gradient / 2, Gradient / 2, index).Also(Fill(edge, max, edge, min));
                        corner.Crop(0, Gradient / 2, Gradient / 2, 0, index).Also(Fill(min, edge, calc(Opacity), min));
                        corner.Crop(Gradient / 2, 0, 0, Gradient / 2, index).Also(Fill(max, max, subtract(max, calc(1 - Opacity)), edge));
                        corner.Crop(Gradient / 2, Gradient / 2, 0, 0, index).Also(Fill(edge, subtract(max, calc(1 - Opacity)), opacity, calc(Opacity)));
                    }
                    else if (condition2())
                    {
                        if (!nonEdges)
                        {
                            corner.Also(Fill(edge, min, opacity, max));
                            return;
                        }

                        corner.Crop(0, 0, Gradient / 2, Gradient / 2, index).Also(Fill(edge, min, edge, max));
                        corner.Crop(0, Gradient / 2, Gradient / 2, 0, index).Also(Fill(max, edge, subtract(max, calc(1 - Opacity)), max));
                        corner.Crop(Gradient / 2, 0, 0, Gradient / 2, index).Also(Fill(min, min, calc(Opacity), edge));
                        corner.Crop(Gradient / 2, Gradient / 2, 0, 0, index).Also(Fill(edge, calc(Opacity), opacity, subtract(max, calc(1 - Opacity))));
                    }
                    else corner.Also(Fill(max, max, opacity, max));
                }

                framePlane.ROI(roi).Min(interFrame);
            }

            return frame;
        }

        public override int SetCacheHints(CacheType cachehints, int frame_range)
        {
            switch (cachehints)
            {
                case CacheType.CACHE_DONT_CACHE_ME:
                    return 1;
                default:
                    return base.SetCacheHints(cachehints, frame_range);
            }
        }

        protected override void Dispose(bool disposing)
        {
            white.Dispose();
            base.Dispose(disposing);
        }
    }
}
