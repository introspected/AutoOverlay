using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AvsFilterNet;

namespace AutoOverlay.Overlay
{
    public record OverlayMapper
    {
        private int frameNumber;
        private OverlayInput input;
        private OverlaySequence sequence;
        private OverlaySequence[] extraSequences;
        private OverlayInfo main;
        private OverlayInfo[] extra;
        private OverlayStabilization stabilization;

        public OverlayMapper(int frameNumber, OverlayInput input, OverlaySequence main, OverlaySequence[] extra, OverlayStabilization stabilization)
        {
            main = main.ScaleBySource(input.SourceSize);
            extra = extra.Select(p => p.ScaleBySource(input.SourceSize)).ToArray();
            this.frameNumber = frameNumber;
            this.input = input;
            this.sequence = main;
            this.extraSequences = extra;
            this.main = main[frameNumber];
            this.extra = extra.Select(p => p[frameNumber]).ToArray();
            this.stabilization = stabilization;
            if (input.ExtraClips.Count != extra.Length)
                throw new AvisynthException();
        }

        public static OverlayMapper For(
            int frameNumber, 
            OverlayInput input,
            List<OverlayInfo> main, 
            OverlayStabilization stabilization, 
            params List<OverlayInfo>[] extra)
        {
            return new OverlayMapper(frameNumber, input, OverlaySequence.Of(main), extra.Select(OverlaySequence.Of).ToArray(), stabilization);
        }

        public static OverlayMapper For(
            OverlayInput input, 
            OverlayInfo main,
            OverlayStabilization stabilization,
            params OverlayInfo[] extra)
        {
            return new OverlayMapper(main.FrameNumber, input, OverlaySequence.Of(main), extra.Select(OverlaySequence.Of).ToArray(), stabilization);
        }

        public OverlayMapper ForFrame(int frameNumber)
        {
            return new OverlayMapper(frameNumber, input, sequence, extraSequences, stabilization);
        }

        public IEnumerable<OverlayMapper> GetNeighbours() => sequence
            .GetNeighbours(frameNumber, stabilization.DiffTolerance, stabilization.AreaTolerance, stabilization.Length)
            .Select(p => p.FrameNumber)
            .Where(frame => !extraSequences.Any() || extraSequences.All(p => p.HasFrame(frame)))
            .Select(ForFrame);

        public RectangleD GetCanvas()
        {
            if (input.FixedSource)
                return main.SourceRectangle.Expand(input.TargetSize.GetAspectRatio());
            var union = extra
                .Select(p => p.Union)
                .Aggregate(main.Union, (acc, rect) => acc.Union(rect));

            var canvas = union.Expand(input.TargetSize.GetAspectRatio());
            var balanceCoef = (input.OverlayBalance + Space.One) / 2;
            var center = (main.SourceRectangle.Location + main.SourceRectangle.AsSpace() / 2) * balanceCoef.Remaining() +
                         (main.OverlayRectangle.Location + main.OverlayRectangle.AsSpace() / 2) * balanceCoef;

            // Outer bounds
            var borderShares = RectangleD.Difference(canvas, union, false).BorderShares;
            var crop = borderShares.Eval(
                canvas.AsSpace().Repeat(),
                input.OuterBounds,
                (share, length, max) => max > 1 ? Math.Max(length * share - max, 0) : length * Math.Max(0, share - max));
            canvas = canvas.Crop(crop);

            var limit = input.InnerBounds.Eval(canvas.AsSpace().Repeat(), (mult, length) => mult > 1 ? mult : length * mult);
            var diff = RectangleD.Difference(main.SourceRectangle, main.OverlayRectangle, true);

            // Inner bounds
            IEnumerable<RectangleD> IterateCrop(RectangleD canvas, RectangleD limit)
            {
                var crops = new HashSet<RectangleD>();

                void ConditionalAdd(RectangleD emptyArea, params RectangleD[] variants)
                {
                    if (emptyArea.Area > 0 && emptyArea.Intersect(canvas).Area > 0)
                        foreach (var variant in variants)
                            crops.Add(variant);
                }

                ConditionalAdd(diff.LeftTop,
                    RectangleD.FromLTRB(diff.LeftTop.Right - canvas.Left - limit.Left, 0, 0, 0),
                    RectangleD.FromLTRB(0, diff.LeftTop.Bottom - canvas.Top - limit.Top, 0, 0));
                ConditionalAdd(diff.RightTop,
                    RectangleD.FromLTRB(0, 0, canvas.Right - diff.RightTop.Left - limit.Right, 0),
                    RectangleD.FromLTRB(0, diff.RightTop.Bottom - canvas.Top - limit.Top, 0, 0));
                ConditionalAdd(diff.RightBottom,
                    RectangleD.FromLTRB(0, 0, canvas.Right - diff.RightBottom.Left - limit.Right, 0),
                    RectangleD.FromLTRB(0, 0, 0, canvas.Bottom - diff.RightBottom.Top - limit.Bottom));
                ConditionalAdd(diff.LeftBottom,
                    RectangleD.FromLTRB(diff.LeftBottom.Right - canvas.Left - limit.Left, 0, 0, 0),
                    RectangleD.FromLTRB(0, 0, 0, canvas.Bottom - diff.LeftBottom.Top - limit.Bottom));

                crops.RemoveWhere(p => !p.LTRB().Any(p => p > RectangleD.EPSILON));
                if (!crops.Any())
                {
                    yield return canvas;
                }

                foreach (var c in crops
                             .Where(p => p.LTRB().Any(p => p > RectangleD.EPSILON))
                             .Select(canvas.Crop)
                             .Select(p => p.Crop(input.TargetSize.GetAspectRatio(), center))
                             .SelectMany(p => IterateCrop(p, limit)))
                {
                    yield return c;
                }
            }


            var maxArea = IterateCrop(canvas, limit)
                .Union(IterateCrop(canvas, RectangleD.Empty))
                .Distinct()
                .Max(p => p.Area);


            return IterateCrop(canvas, limit)
                .Union(IterateCrop(canvas, RectangleD.Empty))
                .Where(p => Math.Abs(p.Area - maxArea) < OverlayConst.EPSILON)
                .Aggregate((acc, c) => acc.Union(c))
                .Crop(input.TargetSize.GetAspectRatio(), center);
        }

        public OverlayData GetOverlayData()
        {
            var canvas = GetCanvas();

            // Stabilization
            var neighbours = GetNeighbours();
            canvas = neighbours
                .Select(p => p.GetCanvas())
                .Aggregate(canvas, RectangleD.Intersect)
                .Crop(canvas.AspectRatio);

            var coef = input.TargetSize.AsSpace() / canvas;

            var offset = -canvas.Location;
            canvas = new RectangleD(Space.Empty, input.TargetSize);

            (Rectangle region, RectangleD crop, Warp warp) GetRegionAndCrop(RectangleD rect, SizeD size, Warp warpIn, double angle)
            {
                var rotate = angle != 0;

                rect = rect.Offset(offset).Scale(coef);
                var targetRect = rotate ? rect.Floor() : RectangleD.Intersect(canvas, rect).Floor();
                var crop = rect.Eval(targetRect, (t, u) => Math.Abs(t - u));
                crop = crop.Scale(size.AsSpace() / rect.Size);
                return (targetRect, crop.IsEmpty ? RectangleD.Empty : crop, warpIn.Scale(coef));
            }

            var (targetSrc, srcCrop, srcWarp) = GetRegionAndCrop(main.SourceRectangle, input.SourceSize, main.SourceWarp, 0);
            var (targetOver, overCrop, overWarp) = GetRegionAndCrop(main.OverlayRectangle, input.OverlaySize, main.OverlayWarp, main.Angle);

            var extraClips = from p in input.ExtraClips.Select((p, i) => Tuple.Create(p.Info.Size, extra[i]))
                             let size = p.Item1
                             let info = p.Item2
                             let regionAndCrop = GetRegionAndCrop(info.OverlayRectangle, size, info.OverlayWarp, info.Angle)
                             select new OverlayData
                             {
                                 Diff = info.Diff,
                                 SourceBaseSize = input.SourceSize,
                                 Source = targetSrc,
                                 SourceCrop = srcCrop,
                                 SourceWarp = srcWarp,
                                 OverlayBaseSize = size,
                                 Overlay = regionAndCrop.region,
                                 OverlayCrop = regionAndCrop.crop,
                                 OverlayAngle = info.Angle,
                                 OverlayWarp = regionAndCrop.warp,
                                 Coef = coef.X,
                             };
            var extraClipList = extraClips.ToList();

            return new OverlayData
            {
                Diff = main.Diff,
                SourceBaseSize = input.SourceSize,
                Source = targetSrc,
                SourceCrop = srcCrop,
                SourceWarp = srcWarp,
                OverlayBaseSize = input.OverlaySize,
                Overlay = targetOver,
                OverlayCrop = overCrop,
                OverlayAngle = main.Angle,
                OverlayWarp = overWarp,
                Coef = coef.X,
                ExtraClips = extraClipList,
                Union = extraClipList
                    .Select(p => p.Overlay)
                    .Aggregate(Rectangle.Union(targetSrc, targetOver), Rectangle.Union)
            }.Also(data => data.ExtraClips.ForEach(p => p.Union = data.Union));
        }
    }
}
