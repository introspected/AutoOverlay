using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace AutoOverlay.Forms
{
    public partial class ProgressDialog : Form
    {
        private Stopwatch watch = new Stopwatch();

        private Func<int, FrameInterval, OverlayInfo> processor;

        private readonly int totalFrames;

        private int processed;

        private IEnumerator<FrameInterval> intervals;

        private FrameInterval interval;

        private IEnumerator<OverlayInfo> frames;

        private bool paused;

        private OverlayEditor editor;

        public ProgressDialog(OverlayEditor editor, ICollection<FrameInterval> intervals, Func<int, FrameInterval, OverlayInfo> processor)
        {
            if (!intervals.Any()
                || intervals.Count > 1 && MessageBox.Show("Are you sure?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.No)
                return;

            InitializeComponent();
            this.processor = processor;
            this.editor = editor;
            totalFrames = intervals.Sum(p => p.Length);
            this.intervals = intervals.GetEnumerator();
            progressBar.Maximum = totalFrames;
        }

        private void UpdateControls(object sender = null, EventArgs e = null)
        {
            var timeRemaining = TimeSpan.FromTicks(watch.ElapsedTicks * (totalFrames - processed) / processed);
            var fps = processed / (watch.ElapsedMilliseconds / 1000.0);
            labelElapsed.Text = $@"Elapsed: {watch.Elapsed:hh\:mm\:ss}";
            labelEta.Text = $@"Remaining: {timeRemaining:hh\:mm\:ss}";
            labelFps.Text = $"FPS: {fps:F2}";
            labelFrames.Text = $"{processed} / {totalFrames}";
            progressBar.Value = processed;
        }

        public void NextFrame()
        {
            if (paused)
                return;
            if (processed == totalFrames)
            {
                Close();
                return;
            }

            processed++;
            Application.DoEvents();

            if (frames == null || !frames.MoveNext())
            {
                interval?.ClearCache();
                if (intervals.MoveNext())
                {
                    interval = intervals.Current;
                    frames = interval.GetEnumerator();
                    frames.MoveNext();
                }
                else
                {
                    Close();
                    return;
                }
            }
            var oldInfo = frames.Current;
            var frame = oldInfo.FrameNumber;

            editor.Post(
                new {frame, interval},
                tuple => processor(tuple.frame, tuple.interval),
                (tuple, info) =>
                {
                    info.Modified = true;
                    if (frame == editor.CurrentFrame)
                        editor.UpdateControls(info);
                    if (interval.Contains(frame - 1) &&
                        (editor.Engine.KeyFrames.Contains(frame) || !info.NearlyEquals(interval[frame - 1], editor.MaxDeviation)))
                    {
                        var newInterval = new FrameInterval(info)
                        {
                            Modified = true
                        };
                        interval.Modified = true;
                        newInterval.AddRange(interval.Where(p => p.FrameNumber > frame).OrderBy(p => p.FrameNumber));
                        interval.RemoveIf(p => p.FrameNumber >= frame);
                        var newIndex = editor.Intervals.IndexOf(interval) + 1;
                        editor.Intervals.Insert(newIndex, newInterval);
                        if (!interval.Any())
                            editor.Intervals.Remove(interval);
                        interval = newInterval;
                        frames = interval.GetEnumerator();
                        frames.MoveNext();
                    }
                    else
                    {
                        if (!oldInfo.Equals(info))
                        {
                            oldInfo.CopyFrom(info);
                            oldInfo.Modified = true;
                        }

                        oldInfo.Diff = info.Diff;
                    }

                    NextFrame();
                });
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            paused = true;
            Close();
        }

        private void btnPause_Click(object sender, EventArgs e)
        {
            watch.Stop();
            timer.Stop();
            btnResume.Visible = true;
            btnPause.Visible = false;
            paused = true;
        }

        private void ProgressDialog_FormClosed(object sender, FormClosedEventArgs e)
        {
            watch.Stop();
            interval?.ClearCache();
            editor.Interval = editor.Intervals.First(p => p.Contains(editor.CurrentFrame));
            editor.Intervals.RaiseListChangedEvents = true;
            editor.Intervals.ResetBindings();
            editor.RenderImpl();
        }

        private void ProgressDialog_Shown(object sender, EventArgs e)
        {
            editor.Intervals.RaiseListChangedEvents = false;
            editor.CheckChanges();
            watch.Start();
            timer.Start();
            NextFrame();
        }

        private void btnResume_Click(object sender, EventArgs e)
        {
            watch.Start();
            timer.Start();
            btnResume.Visible = false;
            btnPause.Visible = true;
            paused = false;
            NextFrame();
        }

        private void ProgressDialog_Load(object sender, EventArgs e)
        {

        }
    }
}
