using System.Collections.Generic;
using System.IO;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AutoOverlay.Filters;
using AvsFilterNet;

[assembly: AvisynthFilterClass(
    typeof(ExtractScenes),
    nameof(ExtractScenes),
    "ss[SceneMinLength]i[MaxDiffIncrease]d",
    OverlayUtils.DEFAULT_MT_MODE)]
namespace AutoOverlay.Filters
{
    public class ExtractScenes : OverlayFilter
    {
        [AvsArgument]
        public string StatFile { get; private set; }

        [AvsArgument]
        public string SceneFile { get; private set; }

        [AvsArgument]
        public int SceneMinLength { get; private set; } = 10;

        [AvsArgument]
        public double MaxDiffIncrease { get; private set; } = 15;

        protected override VideoFrame GetFrame(int n)
        {
            using var overlayStat = new FileOverlayStat(StatFile);
            using var sceneWriter = File.CreateText(SceneFile);

            var totalFrames = 0;
            var totalScenes = 1;
            var scene = new List<OverlayInfo>();
            foreach (var frame in overlayStat.Frames)
            {
                totalFrames++;
                if (scene.Count > SceneMinLength && !OverlayUtils.CheckDev(scene.Append(frame), MaxDiffIncrease, false))
                {
                    scene.Clear();
                    totalScenes++;
                    sceneWriter.WriteLine(frame.FrameNumber);
                }
                else scene.Add(frame);
            }

            return GetSubtitledFrame($"Total scenes: {totalScenes}\nFrames processed: {totalFrames}");
        }
    }
}
