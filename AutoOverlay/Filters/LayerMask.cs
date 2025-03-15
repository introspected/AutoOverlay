using AutoOverlay;
using AutoOverlay.Filters;
using AvsFilterNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using AutoOverlay.AviSynth;
using System.Windows.Forms.VisualStyles;

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
            vi.num_frames = 1;
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

            //return frame;

            // 2. Layers
            foreach (var layer in Layers.Take(LayerIndex).Where((_, i) => !ExcludedLayers.Contains(i)))
            {
                using VideoFrame layerFrame = white[0];
                StaticEnv.MakeWritable(layerFrame);

                var intersection = Rectangle.Intersect(currentLayer, layer);
                intersection.Offset(-currentLayer.X, -currentLayer.Y);
                if (intersection.IsEmpty) continue;

                //var interFrame = framePlane.ROI(intersection);
                var interFrame = new FramePlane(planeChannel, layerFrame, false, intersection, false);

                var opacityColor = (byte)(0xFF * Opacity);
                var nonEdges = GradientEdge == EdgeGradient.NONE;
                var cornerColor = nonEdges ? 0x80 : 0x00;
                var cornerNoise = Noise && nonEdges;

                // 1. Fill intersection area with solid color
                interFrame.Fill(opacityColor);

                // 2. Border gradient if transparent
                // Intersection edges
                Parallel.Invoke([
                    RunIf(intersection.X > 0, 
                        () => interFrame.TakeLeft(Gradient).FillGradient(0xFF, opacityColor, opacityColor, 0xFF, Noise, Seed << 0),
                        () => interFrame.TakeLeft(Gradient).FillGradient(0x00, opacityColor, opacityColor, 0x00, Noise, Seed << 0)),
                    RunIf(intersection.Right < framePlane.width, 
                        () => interFrame.TakeRight(Gradient).FillGradient(opacityColor, 0xFF, 0xFF, opacityColor, Noise, Seed << 1),
                        () => interFrame.TakeRight(Gradient).FillGradient(opacityColor, 0x00, 0x00, opacityColor, Noise, Seed << 1)),
                    RunIf(intersection.Y > 0, 
                        () => interFrame.TakeTop(Gradient).FillGradient(0xFF, 0xFF, opacityColor, opacityColor, Noise, Seed << 2),
                        () => interFrame.TakeTop(Gradient).FillGradient(0x00, 0x00, opacityColor, opacityColor, Noise, Seed << 3)),
                    RunIf(intersection.Bottom < framePlane.height, 
                        () => interFrame.TakeBottom(Gradient).FillGradient(opacityColor, opacityColor, 0xFF, 0xFF, Noise, Seed << 3),
                        () => interFrame.TakeBottom(Gradient).FillGradient(opacityColor, opacityColor, 0x00, 0x00, Noise, Seed << 3))
                ]);

                void FillCorner(int index, FramePlane corner, Func<bool> condition1, Func<bool> condition2)
                {
                    if (condition1() && condition2())
                        corner.FillGradient(0xFF, 0xFF, opacityColor, 0xFF, index, cornerNoise, Seed << index);
                    else if (condition1())
                    {
                        if (!nonEdges)
                        {
                            corner.FillGradient(cornerColor, 0x00, opacityColor, 0xFF, index, cornerNoise, Seed << index);
                            return;
                        }
                        corner
                            .Crop(0, 0, Gradient / 2, Gradient / 2, index)
                            .FillGradient(cornerColor, 0x00, cornerColor, 0xFF, index, cornerNoise, Seed << index);
                        corner.Crop(0, Gradient / 2, Gradient / 2, 0, index)
                            .FillGradient(0xFF, cornerColor, 0xFF - (int)Math.Round(cornerColor * (1 - Opacity)), 0xFF, index, cornerNoise, Seed << index);
                        corner
                            .Crop(Gradient / 2, 0, 0, Gradient / 2, index)
                            .FillGradient(0x00, 0x00, (int)Math.Round(cornerColor * Opacity), cornerColor, index, cornerNoise, Seed << index);
                        corner
                            .Crop(Gradient / 2, Gradient / 2, 0, 0, index)
                            .FillGradient(cornerColor, (int)Math.Round(cornerColor * Opacity), opacityColor, 0xFF - (int)Math.Round(cornerColor * (1 - Opacity)), index, cornerNoise, Seed << index);
                    }
                    else if (condition2())
                    {
                        if (!nonEdges)
                        {
                            corner.FillGradient(cornerColor, 0xFF, opacityColor, 0x00, index, cornerNoise, Seed << index);
                            return;
                        }
                        corner
                            .Crop(0, 0, Gradient / 2, Gradient / 2, index)
                            .FillGradient(cornerColor, 0xFF, cornerColor, 0x00, index, cornerNoise, Seed << index);
                        corner.Crop(0, Gradient / 2, Gradient / 2, 0, index)
                            .FillGradient(0x00, cornerColor, (int)Math.Round(cornerColor * Opacity), 0x00, index, cornerNoise, Seed << index);
                        corner
                            .Crop(Gradient / 2, 0, 0, Gradient / 2, index)
                            .FillGradient(0xFF, 0xFF, 0xFF - (int)Math.Round(cornerColor * (1 - Opacity)), cornerColor, index, cornerNoise, Seed << index);
                        corner
                            .Crop(Gradient / 2, Gradient / 2, 0, 0, index)
                            .FillGradient(cornerColor, 0xFF - (int)Math.Round(cornerColor * (1 - Opacity)), opacityColor, (int)Math.Round(cornerColor * Opacity), index, cornerNoise, Seed << index);
                    }
                    else corner.FillGradient(0x00, 0x00, opacityColor, 0x00, index, cornerNoise, Seed << index);
                }

                Parallel.Invoke([
                    () => FillCorner(0, 
                        interFrame.TakeLeft(Gradient).TakeTop(Gradient),
                        () => intersection.X > 0,
                        () => intersection.Y > 0),
                    () => FillCorner(1,
                        interFrame.TakeRight(Gradient).TakeTop(Gradient),
                        () => intersection.Y > 0,
                        () => intersection.Right < framePlane.width),
                    () => FillCorner(2,
                        interFrame.TakeRight(Gradient).TakeBottom(Gradient),
                        () => intersection.Right < framePlane.width,
                        () => intersection.Bottom < framePlane.height),
                    () => FillCorner(3,
                        interFrame.TakeLeft(Gradient).TakeBottom(Gradient),
                        () => intersection.Bottom < framePlane.height,
                        () => intersection.X > 0),
                ]);


                //if (Noise > 0)
                //{
                //    Parallel.Invoke([
                //        () => interFrame.TakeLeft(Noise).FillNoise(1, 0, 0, 1, intersection.X > 0 ? 255 : 0, Seed << 0),
                //        () => interFrame.TakeRight(Noise).FillNoise(0, 1, 1, 0, intersection.Right < framePlane.width ? 255 : 0, Seed << 1)
                //    ]);
                //    Parallel.Invoke([
                //        () => interFrame.TakeTop(Noise).FillNoise(1, 1, 0, 0, intersection.Y > 0 ? 255 : 0, Seed << 2),
                //        () => interFrame.TakeBottom(Noise).FillNoise(0, 0, 1, 1, intersection.Bottom < framePlane.height ? 255 : 0, Seed << 3)
                //    ]);
                //}


                framePlane.ROI(intersection).Min(interFrame);
            }

            return frame;
        }

        protected override void Dispose(bool disposing)
        {
            white.Dispose();
            base.Dispose(disposing);
        }
    }
}
