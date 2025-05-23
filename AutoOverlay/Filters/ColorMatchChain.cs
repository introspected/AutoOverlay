﻿using System.Collections.Generic;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AvsFilterNet;
using System.Linq;
using System;

[assembly: AvisynthFilterClass(
    typeof(ColorMatchChain), nameof(ColorMatchChain),
    "cc[SampleSpace]s[ReferenceSpace]s[Chain]c[Preset]s[Sample]c[SampleMask]c[ReferenceMask]c[GreyMask]b[Engine]c[SourceCrop]c[OverlayCrop]c" +
    "[Invert]b[Iterations]i[Space]s[Format]s[Resize]s[Length]i[Dither]f[Gradient]f[FrameBuffer]i[FrameDiff]f[FrameMaxDeviation]f" +
    "[Exclude]i[Frames]i*[MatrixConversionHQ]b[InputChromaLocation]s[OutputChromaLocation]s",
    OverlayConst.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class ColorMatchChain : OverlayFilter
    {
        #region Presets
        static ColorMatchChain()
        {
            Dictionary<string, Func<ColorMatchChain, object>> HdrConversion(bool twoSteps) => new()
            {
                [nameof(Chain)] = filter =>
                {
                    var fromType = filter.Child.GetVideoInfo().pixel_type.VPlaneFirst();
                    var fromHdr = fromType.IsHdr();
                    var toType = filter.Reference.GetVideoInfo().pixel_type.VPlaneFirst();
                    var toHdr = toType.IsHdr();
                    if (fromHdr == toHdr)
                        throw new AvisynthException(
                            "Preset HdrConversion could be applied only if one clip has HDR and another has SDR");
                    List<ColorMatchStep> steps =
                    [
                        new()
                        {
                            Sample = "RGBPS",
                            Reference = "RGBPS",
                            Space = fromHdr ? "rgb:st2084:709:f" : "rgb:st2084:2020:f"
                        }
                    ];
                    if (twoSteps)
                    {
                        steps.Add(new()
                        {
                            Sample = fromType.ChangeBitDepth(32).GetName(),
                            Reference = filter.Iterations == 1 ? toType.GetName() : toType.ChangeBitDepth(32).GetName(),
                            Space = fromHdr ? "709:709:709:f" : "2020ncl:st2084:2020:f"
                        });
                    }
                    return steps.ToArray();
                },
                [nameof(SampleSpace)] = filter =>
                {
                    var fromHdr = filter.Child.GetVideoInfo().pixel_type.IsHdr();
                    return fromHdr ? "2020ncl:st2084:2020:f" : "709:709:709:f";
                },
                [nameof(ReferenceSpace)] = filter =>
                {
                    var toHdr = filter.Reference.GetVideoInfo().pixel_type.IsHdr();
                    return toHdr ? "2020ncl:st2084:2020:f" : "709:709:709:f";
                }
            };

            Dictionary<string, Func<ColorMatchChain, object>> SameMatrixConversion(bool rgbFirst, int depth) => new()
            {
                [nameof(Chain)] = filter =>
                {
                    var fromType = filter.Child.GetVideoInfo().pixel_type.VPlaneFirst();
                    var toType = filter.Reference.GetVideoInfo().pixel_type.VPlaneFirst();
                    var steps = new List<ColorMatchStep>();
                    var yuv = new ColorMatchStep
                    {
                        Sample = fromType.ChangeBitDepth(depth).GetName(),
                        Reference = toType.ChangeBitDepth(depth).GetName()
                    };
                    var rgbSpace = depth == 32 ? "RGBPS" : "RGBP" + depth;
                    var rgb = new ColorMatchStep
                    {
                        Sample = rgbSpace,
                        Reference = rgbSpace
                    };
                    if (rgbFirst)
                        steps.AddRange([rgb, yuv]);
                    else steps.AddRange([yuv, rgb]);
                    return steps.ToArray();
                },
                [nameof(SampleSpace)] = filter =>
                {
                    var fromHdr = filter.Child.GetVideoInfo().pixel_type.IsHdr();
                    return fromHdr ? "PC.2020" : "PC.709";
                },
                [nameof(ReferenceSpace)] = filter =>
                {
                    var toHdr = filter.Reference.GetVideoInfo().pixel_type.IsHdr();
                    return toHdr ? "PC.2020" : "PC.709";
                }
            };

            Presets.Add<ColorMatchPreset, ColorMatchChain>(new()
            {
                [ColorMatchPreset.HdrConversion] = HdrConversion(false),
                [ColorMatchPreset.HdrConversionHq] = HdrConversion(true),
                [ColorMatchPreset.RgbYuv32] = SameMatrixConversion(true, 32),
                [ColorMatchPreset.YuvRgb32] = SameMatrixConversion(false, 32),
                [ColorMatchPreset.RgbYuv10] = SameMatrixConversion(true, 10),
                [ColorMatchPreset.YuvRgb10] = SameMatrixConversion(false, 10)
            });
        }
        #endregion

        [AvsArgument(Required = true)]
        public Clip Reference { get; private set; }

        [AvsArgument(NotNull = true)]
        public string SampleSpace { get; private set; }

        [AvsArgument(NotNull = true)]
        public string ReferenceSpace { get; private set; }

        [AvsArgument(NotNull = true)]
        public ColorMatchStep[] Chain { get; private set; }

        [AvsArgument]
        public ColorMatchPreset Preset { get; private set; }

        [AvsArgument]
        public Clip Sample { get; private set; }

        [AvsArgument]
        public Clip SampleMask { get; private set; }

        [AvsArgument]
        public Clip ReferenceMask { get; private set; }

        [AvsArgument]
        public bool GreyMask { get; private set; } = true;

        [AvsArgument]
        public Clip Engine { get; private set; }

        [AvsArgument]
        public Clip SourceCrop { get; protected set; }

        [AvsArgument]
        public Clip OverlayCrop { get; protected set; }

        [AvsArgument]
        public bool Invert { get; private set; }

        [AvsArgument(Min = 1, Max = 10)]
        public int Iterations { get; private set; } = 1;

        [AvsArgument]
        public string Space { get; private set; }

        [AvsArgument]
        public string Format { get; private set; }

        [AvsArgument]
        public string Resize { get; private set; }

        [AvsArgument(Min = 10, Max = 1000000)]
        public int Length { get; set; } = OverlayConst.COLOR_BUCKETS_COUNT;

        [AvsArgument(Min = 0, Max = 1)]
        public double Dither { get; private set; } = 0.95;

        [AvsArgument(Min = 0, Max = 1000000)]
        public double Gradient { get; set; }

        [AvsArgument(Min = 0, Max = OverlayConst.ENGINE_HISTORY_LENGTH)]
        public int FrameBuffer { get; set; }

        [AvsArgument(Min = 0, Max = 100)]
        public double FrameDiff { get; set; } = 1;

        [AvsArgument(Min = 0)]
        public double FrameMaxDeviation { get; protected set; } = 0.5;

        [AvsArgument(Min = 0, Max = 100)]
        public int Exclude { get; set; }

        [AvsArgument]
        public int[] Frames { get; private set; }

        [AvsArgument(Min = 0, Max = 1)]
        public bool MatrixConversionHQ { get; set; }

        [AvsArgument]
        public ChromaLocation? InputChromaLocation { get; set; }

        [AvsArgument]
        public ChromaLocation? OutputChromaLocation { get; set; }

        private string chromaResample;

        private dynamic chain;

        private ClipProperty<int?> colorRange;

        protected override void AfterInitialize()
        {
            if (!Chain.Any())
                throw new AvisynthException("Chain is empty");

            colorRange = new ClipProperty<int?>(Reference, "_ColorRange");

            chromaResample = Resize.GetChromaResample() ?? "spline16";
            Resize ??= StaticEnv.FunctionCoalesce(OverlayConst.DEFAULT_PRESIZE_FUNCTION + "MT", OverlayConst.DEFAULT_PRESIZE_FUNCTION);

            var srcSpace = SampleSpace;
            
            var sample = Sample?.Dynamic();
            chain = Child.Dynamic();
            var chromaIn = InputChromaLocation;
            var chromaOut = OutputChromaLocation;

            dynamic MatchOne(ColorMatchStep step, dynamic chain, dynamic sample, int num)
            {
                step.Reference ??= step.Sample;
                step.Sample ??= step.Reference ??= ((Clip)chain).GetVideoInfo().pixel_type.GetName();
                step.Space ??= srcSpace;

                var length = step.Length == -1 ? Length : step.Length;
                var dither = step.Dither < 0 ? Dither : step.Dither;
                var gradient = step.Gradient < 0 ? Gradient : step.Gradient;
                var frameBuffer = step.FrameBuffer < 0
                    ? (int)(FrameBuffer / Math.Pow(2, num))
                    : step.FrameBuffer;
                var frameDiff = step.FrameDiff < 0 ? FrameDiff : step.FrameDiff;
                var exclude = step.Exclude < 0 ? Exclude : step.Exclude;

                var converted = Convert(chain, srcSpace, step.Space, step.Sample, chromaIn, chromaOut);
                sample = sample == null ? converted : Convert(sample, srcSpace, step.Space, step.Sample, chromaIn, chromaOut);
                var reference = Convert(Reference.Dynamic(), ReferenceSpace, step.Space, step.Reference);

                var sampleMask = ConvertMask(SampleMask?.Dynamic(), step.Sample);
                var referenceMask = ConvertMask(ReferenceMask?.Dynamic(), step.Reference);

                if (Engine == null)
                    return converted.ColorMatch(
                        reference, sample, sampleMask: sampleMask, referenceMask: referenceMask,
                        intensity: step.Intensity, length: length, dither: dither, exclude: exclude, frames: Frames, greyMask: GreyMask,
                        gradient: gradient, frameBuffer: frameBuffer, frameDiff: frameDiff);
                var renderSrc = Invert ? reference : sample;
                var renderOver = Invert ? sample : reference;
                var renderIntensity = Invert ? 1 - step.Intensity : step.Intensity;
                if (Invert)
                    (sampleMask, referenceMask) = (referenceMask, sampleMask);
                return DynamicEnv.OverlayRender(Engine, renderSrc, renderOver, sourceCrop: SourceCrop, overlayCrop: OverlayCrop,
                    sourceMask: sampleMask, overlayMask: referenceMask, colorFrames: Frames,
                    colorMatchTarget: converted, colorAdjust: renderIntensity, invert: Invert, colorBuckets: length,
                    gradientColor: gradient, adjustChannels: step.Channels, preset: nameof(OverlayRenderPreset.FitSource), 
                    upsize: Resize, colorExclude: exclude, colorFramesCount: frameBuffer, colorFramesDiff: frameDiff,
                    colorMaxDeviation: FrameMaxDeviation);
            }

            dynamic Merge(dynamic clip, dynamic merge, ColorMatchStep step)
            {
                if (step.Merge.ChromaWeight.IsNearlyEquals(step.Merge.Weight) && step.Merge.Weight > 0)
                {
                    return clip.Merge(merge, weight: step.Merge.Weight);
                }
                if (step.Merge.Weight > 0)
                    clip = clip.MergeLuma(merge, lumaweight: step.Merge.Weight);
                if (step.Merge.ChromaWeight > 0)
                    clip = clip.MergeChroma(merge, chromaweight: step.Merge.ChromaWeight);
                return clip;
            }

            dynamic MatchTwo(ColorMatchStep step, dynamic chain, dynamic sample, int num)
            {
                var adjusted = MatchOne(step, chain, sample, num);
                if (step.Merge is { Weight: > 0 })
                {
                    if (step.Merge.Merge != null)
                        throw new AvisynthException("Merge max level exceed");
                    var merge = Convert(MatchOne(step.Merge, chain, sample, num), step.Merge.Space ?? srcSpace, step.Space, step.Reference, chromaIn, chromaOut);
                    adjusted = Merge(adjusted, merge, step);
                }
                if (step.Weight < 1)
                    return Merge(chain, adjusted, step);
                return adjusted;
            }

            foreach (var step in Enumerable.Repeat(0, Iterations)
                         .SelectMany(_ => Chain)
                         .Select((p, i) => new { Step = p, Num = i }))
            {
                chain = MatchTwo(step.Step, chain, sample, step.Num);
                if (sample != null)
                    sample = MatchTwo(step.Step, sample, sample, step.Num);
                if (step.Step.Space != null)
                    srcSpace = step.Step.Space;
                chromaIn = chromaOut;
            }

            if (Format == null)
            {
                var size = Child.GetVideoInfo().GetSize();
                var reference = Reference.GetVideoInfo().pixel_type.VPlaneFirst();
                var subsampling = reference.GetSubSample();
                if (size.Width % subsampling.Width > 0 || size.Height % subsampling.Height > 0)
                {
                    reference = (reference & ~ColorSpaces.CS_Sub_Width_Mask) | ColorSpaces.CS_Sub_Width_1;
                    reference = (reference & ~ColorSpaces.CS_Sub_Height_Mask) | ColorSpaces.CS_Sub_Height_1;
                }
                Format = reference.GetName();
            }

            Space ??= ReferenceSpace;
            chain = Convert(chain, srcSpace, Space, Format, chromaOut, chromaOut);

            var vi = ((Clip)chain).GetVideoInfo();
            SetVideoInfo(ref vi);
        }

        protected override VideoFrame GetFrame(int n)
        {
            if (Preset != ColorMatchPreset.Default)
                return colorRange.Write(chain)[n];
            return chain[n];
        }

        private dynamic Convert(dynamic clip, string from, string to, string format, ChromaLocation? chromaIn = null, ChromaLocation? chromaOut = null)
        {
            to ??= from;
            var fromType = ((Clip)clip).GetVideoInfo().pixel_type.VPlaneFirst();
            var toType = format.ParseColorSpace();
            var toRgb = toType.HasFlag(ColorSpaces.CS_BGR);
            var toY = toType.HasFlag(ColorSpaces.CS_GENERIC_Y);
            var same = fromType.HasFlag(ColorSpaces.CS_BGR) == toRgb;
            var convertFunction = toType.GetConvertFunction();

            var chromaInPlacement = chromaIn?.GetAvsName();
            var chromaOutPlacement = toRgb ? null : chromaOut?.GetAvsName();

            dynamic InvokeConvert(dynamic clip, string matrix = null) => toY
                ? clip.Invoke(convertFunction)
                : chromaOutPlacement == null
                    ? clip.Invoke(convertFunction, chromaresample: chromaResample,
                        chromaInPlacement: chromaInPlacement, matrix: matrix)
                    : clip.Invoke(convertFunction, chromaresample: chromaResample,
                        chromaInPlacement: chromaInPlacement, chromaOutPlacement: chromaOutPlacement, matrix: matrix);

            if (from == to)
            {
                if (fromType == toType)
                {
                    if (chromaIn == chromaOut)
                        return clip;
                    return InvokeConvert(clip);
                }
                return InvokeConvert(clip.ConvertBits(toType.GetBitDepth()));
            }

            var toAvsMatrix = AvsUtils.Matrices.Contains(to);
            if (toAvsMatrix)
            {
                var fromDepth = fromType.GetBitDepth();
                var toDepth = toType.GetBitDepth();

                var changeDepthFirst = fromType.GetBitDepth() > toType.GetBitDepth();
                if (!same)
                {
                    if (fromDepth == toDepth)
                        return InvokeConvert(clip, to);
                    if (changeDepthFirst)
                        return InvokeConvert(clip.ConvertBits(toType.GetBitDepth()), to);
                    return InvokeConvert(clip, to).ConvertBits(toType.GetBitDepth());
                }

                if (toRgb)
                    return clip;

                if (MatrixConversionHQ && fromDepth != 32)
                    clip = clip.ConvertBits(32);

                clip = clip.ConvertToPlanarRgb(matrix: from, chromaInPlacement: chromaInPlacement, chromaResample: chromaResample);
                clip = InvokeConvert(clip, to);

                if (MatrixConversionHQ && toDepth != 32)
                    clip = clip.ConvertBits(toDepth);

                return clip;
            }
            if (toType.HasFlag(ColorSpaces.CS_BGR | ColorSpaces.CS_INTERLEAVED | ColorSpaces.CS_RGBA_TYPE))
                clip = clip.ConvertToPlanarRGBA();
            else if (toType.HasFlag(ColorSpaces.CS_BGR | ColorSpaces.CS_INTERLEAVED | ColorSpaces.CS_RGB_TYPE))
                clip = clip.ConvertToPlanarRGB();

            string chromaloc = null;
            if (toRgb && chromaIn.HasValue)
                chromaloc = $"{chromaIn}=>{chromaIn}";
            if (!toRgb && chromaOut.HasValue)
                chromaloc = $"{chromaIn?.ToString() ?? "auto"}=>{chromaOut.ToString()}";

            return clip.z_ConvertFormat(pixel_type: format, use_props: 1,
                colorspace_op: $"{from}=>{to}", chromaloc_op: chromaloc, 
                dither_type: "ordered", resample_filter_uv: chromaResample);
        }

        private dynamic ConvertMask(dynamic mask, string format)
        {
            if (mask == null)
                return null;
            var fromType = ((Clip)mask).GetVideoInfo().pixel_type.VPlaneFirst();
            var toType = format.ParseColorSpace();
            if (fromType == toType)
                return mask;
            var toRgb = toType.HasFlag(ColorSpaces.CS_BGR);
            var same = fromType.HasFlag(ColorSpaces.CS_BGR) == toRgb;
            mask = mask.ConvertBits(toType.GetBitDepth(), fulls: true);
            if (same)
                return mask;
            if (toRgb)
                return mask.ConvertToPlanarRgb();
            return mask.ConvertToYUV444(matrix: "Average");
        }

        protected override void Dispose(bool disposing)
        {
            chain?.Dispose();
            base.Dispose(disposing);
        }
    }
}
