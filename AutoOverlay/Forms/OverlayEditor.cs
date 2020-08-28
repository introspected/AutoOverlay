using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AvsFilterNet;

namespace AutoOverlay
{
    public partial class OverlayEditor : Form
    {
        private readonly KeyboardHook keyboardHook = new KeyboardHook(true);

        private readonly OverlayEngine engine;

        private bool captured;

        private int _currentFrame;

        private BindingList<FrameInterval> Intervals { get; } = new BindingList<FrameInterval>();

        private volatile FrameInterval prevInterval;

        private bool update;

        private bool NeedSave => Intervals.Any(p => p.Modified);

        private readonly ScriptEnvironment env;

        private bool panscan;

        private bool autoOverlay;

        private string compareFilename;

        private int CurrentFrame
        {
            get => _currentFrame;
            set
            {
                if (value == CurrentFrame && pictureBox.Image != null) return;
                if (!(value >= nudMinFrame.Value && value <= nudMaxFrame.Value) ||
                    !(value >= nudCurrentFrame.Minimum && value <= nudCurrentFrame.Maximum))
                {
                    pictureBox.SuspendLayout();
                    pictureBox.Image?.Dispose();
                    pictureBox.Image = null;
                    pictureBox.ResumeLayout();
                    return;
                }
                nudCurrentFrame.Value = trackBar.Value = _currentFrame = value;
                Cursor.Current = Cursors.WaitCursor;
                if (Interval == null || !Interval.Contains(value))
                {
                    var interval = Intervals.FirstOrDefault(p => p.Contains(value));
                    if (interval == null)
                    {
                        using (new DynamicEnvironment(env))
                        {
                            var info = engine.GetOverlayInfo(value);
                            interval = new FrameInterval
                            {
                                Frames = {info}
                            };
                        }
                        InsertInterval(interval);
                    }
                    Interval = interval;
                }
                RenderImpl();
                Cursor.Current = Cursors.Default;
            }
        }

        private void InsertInterval(FrameInterval interval)
        {
            var index = Intervals.Count(p => p.First < interval.First);
            Intervals.Insert(index, interval);
        }

        private FrameInterval Interval
        {
            get => grid.BindingContext[Intervals].Position >= 0 ? grid?.BindingContext?[Intervals].Current as FrameInterval : null;
            set => grid.BindingContext[Intervals].Position = value == null ? -1 : Intervals.IndexOf(value);
        }

        private OverlayInfo FrameInfo => Interval?.Frames.FirstOrDefault(p => p.FrameNumber == CurrentFrame);

        private void UpdateInterval(object sender = null, EventArgs e = null)
        {
            if (Interval == prevInterval) return;
            CheckChanges(prevInterval);
            prevInterval = Interval;
            if (Interval != null)
            {
                UpdateControls(Interval);
                if (!Interval.Contains(CurrentFrame))
                    CurrentFrame = Interval.First;
                else RenderImpl();
            }
        }

        private void CheckChanges(FrameInterval interval)
        {
            if (interval == null) return;
            var info = GetOverlayInfo();
            if (!info.Equals(interval))
            {
                interval.CopyFrom(info);
                interval.Modified = true;
                grid.Refresh();
            }
        }

        private void UpdateControls(AbstractOverlayInfo info)
        {
            if (!(panelManage.Enabled = info != null))
                return;

            update = true;

            nudX.Value = info.X;
            nudY.Value = info.Y;
            nudAngle.Value = (decimal)(info.Angle/100.0);
            nudOverlayWidth.Value = info.Width;
            nudOverlayHeight.Value = info.Height;
            var crop = info.GetCrop();
            nudCropLeft.Value = (decimal) crop.Left;
            nudCropTop.Value = (decimal) crop.Top;
            nudCropRight.Value = (decimal) crop.Right;
            nudCropBottom.Value = (decimal) crop.Bottom;
            chbOverlaySizeSync.Checked = Round(engine.OverInfo.Width / info.GetAspectRatio(engine.OverInfo.Size)) == engine.OverInfo.Height;

            update = false;
        }

        private static int Round(double value) => (int)Math.Round(value);

        #region Form behavior
        public OverlayEditor(OverlayEngine engine, ScriptEnvironment env)
        {
            this.engine = engine;
            this.env = env;
            InitializeComponent();
            cbMode.Items.AddRange(Enum.GetValues(typeof(FramingMode)).Cast<object>().ToArray());
            cbMode.SelectedItem = FramingMode.Fit;
            cbOverlayMode.SelectedItem = "Blend";
            cbMatrix.Items.AddRange(Enum.GetValues(typeof(AvsMatrix)).Cast<object>().ToArray());
            cbMatrix.Enabled = chbRGB.Enabled = !engine.SrcInfo.Info.IsRGB();
            if (chbRGB.Enabled)
            {
                cbMatrix.SelectedItem = AvsMatrix.Rec709;
            }
            else
            {
                chbRGB.Checked = true;
                cbMatrix.SelectedItem = AvsMatrix.Default;
            }
            nudOutputWidth.Value = engine.SrcInfo.Width;
            nudOutputHeight.Value = engine.SrcInfo.Height;
            nudMaxFrame.Value = nudMinFrame.Maximum = nudMaxFrame.Maximum = engine.GetVideoInfo().num_frames - 1;
            engine.CurrentFrameChanged += OnCurrentFrameChanged;
            keyboardHook.KeyDown += keyboardHook_KeyDown;
            Closing += (o, e) => keyboardHook.Dispose();
        }

        private void keyboardHook_KeyDown(object sender, KeyEventArgs e)
        {
            if (ActiveForm != this) return;
            e.Handled = true;
            switch (e.KeyData)
            {
                case Keys.Enter:
                    if (ActiveControl is NumericUpDown nud)
                    {
                        var val = int.Parse(nud.Text);
                        if (val != (int)nud.Value)
                            nud.Value = val;
                    }
                    break;
                case Keys.S | Keys.Control:
                    SaveStat();
                    break;
                case Keys.R | Keys.Control:
                    ResetCurrent();
                    break;
                case Keys.D:
                    chbPreview.Checked = true;
                    cbOverlayMode.SelectedItem = "Difference".Equals(cbOverlayMode.SelectedItem) ? "Blend" : "Difference";
                    RenderImpl();
                    break;
                case Keys.P:
                    chbPreview.Checked = !chbPreview.Checked;
                    RenderImpl();
                    break;
                case Keys.Left | Keys.Control:
                    nudX.Value--;
                    break;
                case Keys.Right | Keys.Control:
                    nudX.Value++;
                    break;
                case Keys.Up | Keys.Control:
                    nudY.Value--;
                    break;
                case Keys.Down | Keys.Control:
                    nudY.Value++;
                    break;
                case Keys.Add | Keys.Control:
                    nudOverlayWidth.Value++;
                    break;
                case Keys.Subtract | Keys.Control:
                    nudOverlayWidth.Value--;
                    break;
                case Keys.A:
                    CurrentFrame++;
                    break;
                case Keys.Z:
                    CurrentFrame--;
                    break;
                case Keys.Home:
                    if (Interval != null)
                        CurrentFrame = Interval.First;
                    break;
                case Keys.End:
                    if (Interval != null)
                        CurrentFrame = Interval.Last;
                    break;
                case Keys.PageDown:
                    if (Interval != null)
                    {
                        var src = grid.BindingContext[Intervals];
                        if (src.Position < Intervals.Count - 1)
                        {
                            src.Position++;
                        }
                    }
                    break;
                case Keys.PageUp:
                    if (Interval != null)
                    {
                        var src = grid.BindingContext[Intervals];
                        if (src.Position > 0)
                        {
                            CurrentFrame = Intervals[src.Position - 1].Last;
                        }
                    }
                    break;
                default:
                    e.Handled = false;
                    break;
            }
        }

        private void LoadStat(bool initial = false)
        {
            Cursor.Current = Cursors.WaitCursor;
            Intervals.RaiseListChangedEvents = false;
            Intervals.Clear();
            if (engine?.OverlayStat == null) return;
            FrameInterval lastInterval = null;
            OverlayInfo lastFrame = null;
            var maxDeviation = (double) nudDeviation.Value;
            var min = (int) nudMinFrame.Value;
            var max = (int) nudMaxFrame.Value;
            var overSize = engine.OverInfo.Size;
            var compareLimit = (double) nudCompare.Value;
            using (var refStat = compareFilename == null ? null : new FileOverlayStat(compareFilename, engine.SrcInfo.Size, engine.OverInfo.Size))
                foreach (var info in engine.OverlayStat.Frames.Where(p => p.FrameNumber >= min && p.FrameNumber <= max))
                {
                    info.Comparison = refStat?[info.FrameNumber]?.Compare(info, engine.SrcInfo.Size) ?? 2;
                    var compareFailed = info.Comparison < compareLimit;
                    var diffFailed = info.Diff > engine.MaxDiff;
                    if (chbDefective.Checked && !diffFailed && !compareFailed)
                        continue;
                    if (lastInterval == null || lastFrame.FrameNumber != info.FrameNumber - 1 || !lastFrame.NearlyEquals(info, overSize, maxDeviation))
                    {
                        if (initial && Intervals.Count == 1000)
                        {
                            nudMaxFrame.ValueChanged -= Reset;
                            nudMaxFrame.Value = lastInterval.Frames.Last().FrameNumber;
                            nudMaxFrame.ValueChanged += Reset;
                            break;
                        }
                        lastInterval = new FrameInterval();
                        Intervals.Add(lastInterval);

                    }
                    lastInterval.Frames.Add(info);
                    lastFrame = info;
                }
            Intervals.RaiseListChangedEvents = true;
            Interval = Intervals.FirstOrDefault(p => p.Contains(CurrentFrame));
            Intervals.ResetBindings();
            nudCurrentFrame.ValueChanged -= nudCurrentFrame_ValueChanged;
            nudCurrentFrame.Minimum = trackBar.Minimum = (int) nudMinFrame.Value;
            nudCurrentFrame.Maximum = trackBar.Maximum = (int) nudMaxFrame.Value;
            CurrentFrame = (int) nudCurrentFrame.Value;
            Interval = Intervals.FirstOrDefault(p => p.Contains(CurrentFrame));
            nudCurrentFrame.ValueChanged += nudCurrentFrame_ValueChanged;

            UpdateControls(Interval);
            grid.Refresh();
            Cursor.Current = Cursors.Default;
        }

        private void OverlayEditorForm_Load(object sender, EventArgs e)
        {
            LoadStat(true);
            grid.DataSource = Intervals;
            grid.BindingContext[Intervals].CurrentChanged += UpdateInterval;
        }

        private void OverlayEditorForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            engine.CurrentFrameChanged -= OnCurrentFrameChanged;
            pictureBox.Image?.Dispose();
        }

        private void OnCurrentFrameChanged(object sender, FrameEventArgs args)
        {
            CurrentFrame = args.FrameNumber;
        }
        #endregion

        #region Frame management
        private volatile object sync;

        private void Render(object sender = null, EventArgs e = null)
        {
            if (update || FrameInfo == null) return;
            if ((sender == chbOverlaySizeSync || sender == nudOverlayWidth) && chbOverlaySizeSync.Checked)
            {
                nudOverlayHeight.ValueChanged -= Render;
                nudOverlayHeight.Value = Round((double)nudOverlayWidth.Value / FrameInfo.GetAspectRatio(engine.OverInfo.Size));
                nudOverlayHeight.ValueChanged += Render;
            }
            else if (sender == nudOverlayHeight && chbOverlaySizeSync.Checked)
            {
                nudOverlayWidth.ValueChanged -= Render;
                nudOverlayWidth.Value = Round((double)nudOverlayHeight.Value * FrameInfo.GetAspectRatio(engine.OverInfo.Size));
                nudOverlayWidth.ValueChanged += Render;
            }
            Task.Factory.StartNew(() =>
            {
                var obj = sync = new object();
                Thread.Sleep(500);
                if (obj == sync)
                    pictureBox.SafeInvoke(p => RenderImpl());
            });
        }

        private OverlayInfo GetOverlayInfo()
        {
            Func<decimal, int> crop = val => (int) (val * OverlayInfo.CROP_VALUE_COUNT);
            return new OverlayInfo
            {
                FrameNumber = CurrentFrame,
                X = (int) nudX.Value,
                Y = (int) nudY.Value,
                Angle = (int) (nudAngle.Value*100),
                Width = (int) nudOverlayWidth.Value,
                Height = (int) nudOverlayHeight.Value,
                CropLeft = crop(nudCropLeft.Value),
                CropTop = crop(nudCropTop.Value),
                CropRight = crop(nudCropRight.Value),
                CropBottom = crop(nudCropBottom.Value),
                SourceWidth = FrameInfo?.SourceWidth ?? engine.SrcInfo.Width,
                SourceHeight = FrameInfo?.SourceHeight ?? engine.SrcInfo.Height,
                BaseWidth = FrameInfo?.BaseWidth ?? engine.OverInfo.Width,
                BaseHeight = FrameInfo?.BaseHeight ?? engine.OverInfo.Height,
                Diff = FrameInfo?.Diff ?? -1
            };
        }

        private void RenderImpl()
        {
            using (dynamic invoker = new DynamicEnvironment(env))
            using (new VideoFrameCollector())
            {
                var info = GetOverlayInfo();
                var outSize = new Size((int) nudOutputWidth.Value, (int) nudOutputHeight.Value);
                var crop = info.GetCrop();
                {
                    var outClip = chbPreview.Checked
                        ? invoker.StaticOverlayRender(
                            engine.Source, engine.Overlay,
                            info.X, info.Y, info.Angle / 100.0,
                            info.Width, info.Height,
                            crop.Left, crop.Top, crop.Right, crop.Bottom,
                            diff: info.Diff,
                            sourceMask: engine.SourceMask,
                            overlayMask: engine.OverlayMask,
                            width: outSize.Width,
                            height: outSize.Height,
                            gradient: (int) nudGradientSize.Value,
                            noise: (int) nudNoiseSize.Value,
                            dynamicNoise: true,
                            mode: (int) cbMode.SelectedItem,
                            overlayMode: cbOverlayMode.SelectedItem,
                            opacity: tbOpacity.Value / 100.0,
                            colorAdjust: chbColorAdjust.Checked ? tbColorAdjust.Value / 100.0 : -1,
                            matrix: chbRGB.Checked && cbMatrix.Enabled && cbMatrix.SelectedIndex > 0
                                ? cbMatrix.SelectedItem.ToString()
                                : string.Empty,
                            upsize: engine.Resize,
                            downsize: engine.Resize,
                            rotate: engine.Rotate,
                            debug: chbDebug.Checked,
                            invert: false)
                        : engine.ResizeRotate(engine.Source, engine.Resize, engine.Rotate, outSize.Width, outSize.Height);
                    if (cbMatrix.Enabled)
                    {
                        outClip = cbMatrix.SelectedItem.Equals(AvsMatrix.Default)
                            ? outClip.ConvertToRGB24()
                            : outClip.ConvertToRGB24(matrix: cbMatrix.SelectedItem.ToString());
                    }
                    VideoFrame frame = outClip[CurrentFrame];
                    var res = new Bitmap(frame.ToBitmap(PixelFormat.Format24bppRgb));
                    pictureBox.SuspendLayout();
                    pictureBox.Image?.Dispose();
                    pictureBox.Image = res;
                    pictureBox.ResumeLayout();
                }
            }
        }
        #endregion

        #region Trackbar
        private void trackBar_Scroll(object sender, EventArgs e)
        {
            if (captured) return;
            Application.DoEvents();
            CurrentFrame = trackBar.Value;
        }

        private void trackBar_MouseDown(object sender, MouseEventArgs e)
        {
            captured = true;
        }

        private void trackBar_MouseUp(object sender, MouseEventArgs e)
        {
            if (captured)
            {
                captured = false;
                Application.DoEvents();
                CurrentFrame = trackBar.Value;
            }
        }
        #endregion

        private void chbEditor_CheckedChanged(object sender, EventArgs e)
        {
            panelManage.Visible = chbEditor.Checked;
            pictureBox.SizeMode = chbEditor.Checked ? PictureBoxSizeMode.Zoom : PictureBoxSizeMode.CenterImage;
        }

        private void grid_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            var compareLimit = (double) nudCompare.Value;
            if (e.RowIndex < 1) return;
            var item = Intervals[e.RowIndex];
            if (item.Modified)
                grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
            else if (item.Comparison < compareLimit)
                grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.SkyBlue;
            else if (item.Diff > engine.MaxDiff)
                grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightCoral;
            else if (item.Frames.Any(p => p.Diff > engine.MaxDiff))
                grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightPink;
        }

        private void ResetCurrent(object sender = null, EventArgs e = null)
        {
            var info = engine.OverlayStat[CurrentFrame];
            Interval[CurrentFrame].Diff = info.Diff;
            UpdateControls(info);
            RenderImpl();
        }

        private void Reset(object sender = null, EventArgs e = null)
        {
            Task.Factory.StartNew(() =>
            {
                var obj = sync = new object();
                Thread.Sleep(500);
                if (obj == sync)
                    this.SafeInvoke(p => ResetImpl());
            });
        }

        private void ResetImpl()
        {
            if (NeedSave)
            {
                var answer = MessageBox.Show("Save changes before reload?", "Warning",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button3);
                if (answer == DialogResult.Cancel)
                    return;
                if (answer == DialogResult.Yes)
                    SaveStat();
            }
            LoadStat();
        }

        private void SaveStat(object sender = null, EventArgs e = null)
        {
            var watch = new Stopwatch();
            watch.Start();
            Cursor.Current = Cursors.WaitCursor;
            CheckChanges(Interval);
            watch.Stop();
            Debug.WriteLine($"Check changes: {watch.ElapsedMilliseconds}");
            watch.Restart();
            var frames = Intervals.Where(p => p.Modified).SelectMany(p => p.Frames);
            engine.OverlayStat.Save(frames.ToArray());
            watch.Stop();
            Debug.WriteLine($"Save changes: {watch.ElapsedMilliseconds}");
            watch.Restart();
            foreach (var interval in Intervals)
                interval.Modified = false;
            Intervals.ResetBindings();
            grid.Refresh();
            watch.Stop();
            Debug.WriteLine($"Refresh: {watch.ElapsedMilliseconds}");
            Cursor.Current = Cursors.Default;
        }

        private void btnAutoOverlaySingleFrame_Click(object sender, EventArgs e)
        {
            if (Interval == null) return;
            Cursor.Current = Cursors.WaitCursor;
            using (new DynamicEnvironment(env))
            using (new VideoFrameCollector())
            {
                var info = engine.AutoOverlayImpl(CurrentFrame);
                Interval[CurrentFrame].Diff = info.Diff;
                UpdateControls(info);
            }
            RenderImpl();
            Cursor.Current = Cursors.Default;
        }

        private void btnSeparate_Click(object sender, EventArgs e)
        {
            if (FrameInfo == null || Interval.Length <= 1) return;
            CheckChanges(Interval);
            var parent = Interval;
            Intervals.RaiseListChangedEvents = false;
            var current = new FrameInterval
            {
                Modified = true,
                Frames = { FrameInfo }
            };
            Intervals.Remove(parent);
            if (CurrentFrame != parent.First)
            {
                var prev = new FrameInterval
                {
                    Modified = true
                };
                prev.Frames.AddRange(parent.Frames.Where(p => p.FrameNumber < CurrentFrame));
                InsertInterval(prev);
            }
            InsertInterval(current);
            if (CurrentFrame != parent.Last)
            {
                var next = new FrameInterval
                {
                    Modified = true
                };
                next.Frames.AddRange(parent.Frames.Where(p => p.FrameNumber > CurrentFrame));
                InsertInterval(next);
            }
            Intervals.RaiseListChangedEvents = true;
            Intervals.ResetBindings();
            Interval = current;
            grid.Refresh();
        }

        private void nudCurrentFrame_ValueChanged(object sender, EventArgs e)
        {
            CurrentFrame = (int) nudCurrentFrame.Value;
        }

        private void nudCurrentFrame_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                nudCurrentFrame.Value = int.Parse(nudCurrentFrame.Text);
        }

        private void SuppressKeyPress(object sender, KeyEventArgs e)
        {
            e.Handled = true;
        }

        private void btnJoinNext_Click(object sender, EventArgs e)
        {
            JoinNext();
            grid.Refresh();
        }

        private void btnJoinPrev_Click(object sender, EventArgs e)
        {
            JoinPrev();
            grid.Refresh();
        }

        private void JoinPrev()
        {
            if (Interval == null) return;
            var prevIndex = grid.BindingContext[Intervals].Position - 1;
            if (prevIndex == -1) return;
            var prev = Intervals[prevIndex];
            if (prev.Last != Interval.First - 1) return;
            prev.CopyFrom(Interval);
            Interval.Frames.AddRange(prev.Frames);
            Interval.Modified = true;
            Intervals.Remove(prev);
        }

        private void JoinNext()
        {
            if (Interval == null) return;
            var nextIndex = grid.BindingContext[Intervals].Position + 1;
            if (nextIndex == Intervals.Count) return;
            var next = Intervals[nextIndex];
            if (next.First != Interval.Last + 1) return;
            Intervals.Remove(next);
            next.CopyFrom(Interval);
            Interval.Frames.AddRange(next.Frames);
            Interval.Modified = true;
        }

        private void btnJoinTo_Click(object sender, EventArgs e)
        {
            if (Interval == null) return;
            var nud = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = engine.GetVideoInfo().num_frames - 1,
                Value = CurrentFrame
            };
            var form = new Form
            {
                Controls = { nud },
                Text = "Enter frame number",
                StartPosition = FormStartPosition.CenterScreen,
                Size = new Size(200, 100)
            };
            nud.KeyDown += (obj, args) =>
            {
                if (args.KeyData == Keys.Enter)
                {
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                }
            };
            nud.Enter += (obj, args) => nud.Select(0, nud.Text.Length);
            if (form.ShowDialog(this) != DialogResult.OK) return;
            var frame = (int)nud.Value;
            var join = Intervals.FirstOrDefault(p => p.Contains(frame));
            if (Interval == join) return;
            if (join == null)
            {
                MessageBox.Show("Selected frame is not processed yet", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Intervals.RaiseListChangedEvents = false;
            do
            {
                if (CurrentFrame > frame)
                {
                    JoinPrev();
                    grid.BindingContext[Intervals].Position--;
                }
                else JoinNext();
            } while (!Interval.Contains(frame));
            Intervals.RaiseListChangedEvents = true;
            Intervals.ResetBindings();
            grid.Refresh();
        }

        private void btnAutoOverlayScene_Click(object sender, EventArgs e)
        {
            if (autoOverlay)
            {
                autoOverlay = false;
                return;
            }

            if (Interval == null) return;
            Cursor.Current = Cursors.WaitCursor;
            Intervals.RaiseListChangedEvents = false;
            var lastInterval = Interval;
            var text = Text;
            var btnText = btnAutoOverlayScene.Text;
            btnAutoOverlayScene.Text = "Cancel";
            var first = Interval.First;
            var length = Interval.Length;
            var startTime = DateTime.Now;
            autoOverlay = true;
            for (int frame = Interval.First, last = Interval.Last; frame <= last; frame++)
            {
                if (!autoOverlay)
                    break;
                var processed = frame - first + 1;
                var timeRemaining = TimeSpan.FromTicks(DateTime.Now.Subtract(startTime).Ticks * (length - processed) / processed);
                Text = $"{text}: {processed}/{length} {(processed * 100.0) / length:F2}% ETA: {timeRemaining:T}";
                Application.DoEvents();
                using (new DynamicEnvironment(env))
                using (new VideoFrameCollector())
                {
                    var info = engine.AutoOverlayImpl(frame);
                    info.FrameNumber = frame;
                    if (frame == CurrentFrame)
                        UpdateControls(info);
                    if (!info.Equals(lastInterval))
                    {
                        var newInterval = new FrameInterval
                        {
                            Frames = { info },
                            Modified = true
                        };
                        lastInterval.Modified = true;
                        newInterval.Frames.AddRange(lastInterval.Frames.Where(p => p.FrameNumber > frame)
                            .OrderBy(p => p.FrameNumber));
                        lastInterval.Frames.RemoveAll(p => p.FrameNumber >= frame);
                        var newIndex = Intervals.IndexOf(lastInterval) + 1;
                        Intervals.Insert(newIndex, newInterval);
                        if (lastInterval.Frames.Count == 0)
                            Intervals.Remove(lastInterval);
                        lastInterval = newInterval;
                    }
                    else
                    {
                        var oldInfo = lastInterval.Frames.First(p => p.FrameNumber == frame);
                        oldInfo.CopyFrom(info);
                        oldInfo.Diff = info.Diff;
                    }
                }
            }
            autoOverlay = false;
            Text = text;
            btnAutoOverlayScene.Text = btnText;
            Interval = Intervals.First(p => p.Contains(CurrentFrame));
            Intervals.RaiseListChangedEvents = true;
            Intervals.ResetBindings();
            RenderImpl();
            Cursor.Current = Cursors.Default;
        }

        private void btnAutoOverlaySeparatedFrame_Click(object sender, EventArgs e)
        {
            if (Interval == null) return;
            Cursor.Current = Cursors.WaitCursor;
            using (new DynamicEnvironment(env))
            using (new VideoFrameCollector())
            {
                var info = engine.AutoOverlayImpl(CurrentFrame);
                Interval[CurrentFrame].Diff = info.Diff;
                if (!info.Equals(Interval))
                {
                    btnSeparate_Click(sender, e);
                    UpdateControls(info);
                }
            }
            RenderImpl();
            Cursor.Current = Cursors.Default;
        }

        private void btnResetCrop_Click(object sender, EventArgs e)
        {
            nudCropLeft.Value = nudCropTop.Value = nudCropRight.Value = nudCropBottom.Value = 0;
        }

        private void brnPanScan_Click(object sender, EventArgs e)
        {
            if (Interval == null) return;
            PanScan(new List<FrameInterval> {Interval}, btnPanScan);
        }

        private void btnPanScanFull_Click(object sender, EventArgs e)
        {
            PanScan(Intervals.Where(p => p.Length >= engine.BackwardFrames).ToList(), btnPanScanFull);
        }

        private void PanScan(ICollection<FrameInterval> intervals, Button button)
        {
            if (panscan)
            {
                panscan = false;
                return;
            }

            if (!intervals.Any() || intervals.Count > 1
                && MessageBox.Show("Are you sure?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) ==
                DialogResult.No) return;

            panscan = true;

            var textOld = button.Text;
            button.Text = "Cancel";
            Cursor.Current = Cursors.WaitCursor;
            Intervals.RaiseListChangedEvents = false;
            CheckChanges(Interval);
            var text = Text;
            var length = intervals.Sum(p => p.Length);
            var processed = 0;
            var startTime = DateTime.Now;
            foreach (var interval in intervals)
            {
                var lastInterval = interval;

                for (int frame = interval.First, last = interval.Last; frame <= last; frame++)
                {
                    if (!panscan)
                        break;
                    processed++;
                    var timeRemaining = TimeSpan.FromTicks(DateTime.Now.Subtract(startTime).Ticks * (length - processed) / processed);
                    Text = $"{text}: {processed}/{length} {(processed * 100.0) / length:F2}% ETA: {timeRemaining:T}";
                    Application.DoEvents();
                    using (new DynamicEnvironment(env))
                    using (new VideoFrameCollector())
                    {
                        var delta = (int)nudDistance.Value;
                        var scale = (double)nudScale.Value / 1000;
                        var info = engine.PanScanImpl(lastInterval, frame, delta, scale, false);
                        var oldInfo = lastInterval.Frames.First(p => p.FrameNumber == frame);
                        //if (oldInfo.Diff < info.Diff)
                        //    continue;
                        //engine.OverlayStat[info.FrameNumber] = info;
                        if (frame == CurrentFrame)
                            UpdateControls(info);
                        if (!info.Equals(lastInterval))
                        {
                            var newInterval = new FrameInterval
                            {
                                Frames = { info },
                                Modified = true
                            };
                            lastInterval.Modified = true;
                            newInterval.Frames.AddRange(lastInterval.Frames.Where(p => p.FrameNumber > frame).OrderBy(p => p.FrameNumber));
                            lastInterval.Frames.RemoveAll(p => p.FrameNumber >= frame);
                            var newIndex = Intervals.IndexOf(lastInterval) + 1;
                            Intervals.Insert(newIndex, newInterval);
                            if (lastInterval.Frames.Count == 0)
                                Intervals.Remove(lastInterval);
                            lastInterval = newInterval;
                        }
                        else
                        {
                            oldInfo.CopyFrom(info);
                            oldInfo.Diff = info.Diff;
                        }
                    }
                }
            }
            panscan = false;
            button.Text = textOld;
            Text = text;
            Interval = Intervals.First(p => p.Contains(CurrentFrame));
            Intervals.RaiseListChangedEvents = true;
            Intervals.ResetBindings();
            RenderImpl();
            Cursor.Current = Cursors.Default;
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (Interval == null || MessageBox.Show(
                    "Selected frames will be deleted. Continue?", "Warning",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.No) return;
            for (var frame = Interval.First; frame <= Interval.Last; frame++)
                engine.OverlayStat[frame] = null;
            Intervals.Remove(Interval);
            grid.Refresh();
        }

        private void grid_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            var interval = Intervals[e.RowIndex];
            var fieldName = grid.Columns[e.ColumnIndex].Name;
            e.Value = interval.GetType().GetField(fieldName).GetValue(interval);
        }

        private void btnCompare_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;
            compareFilename = openFileDialog1.FileName;
            Compare();
        }

        private void Compare(object sender = null, EventArgs e = null)
        {
            if (compareFilename == null) return;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                using (var refStat = new FileOverlayStat(compareFilename, engine.SrcInfo.Size, engine.OverInfo.Size))
                    foreach (var frame in Intervals.SelectMany(p => p.Frames))
                    {
                        var refFrame = refStat[frame.FrameNumber];
                        if (refFrame == null) continue;
                        frame.Comparison = refFrame.Compare(frame, engine.OverInfo.Size);
                    }
                grid.Refresh();
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        private void OverlayEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (NeedSave)
            {
                var answer = MessageBox.Show("Save changes before exit?", "Warning",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button3);
                if (answer == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                if (answer == DialogResult.Yes)
                    SaveStat();
            }
        }
    }
}
