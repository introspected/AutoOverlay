using System;
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

        private int CurrentFrame
        {
            get => _currentFrame;
            set
            {
                if (value == CurrentFrame) return;
                nudCurrentFrame.Value = trackBar.Value = _currentFrame = value;
                Cursor.Current = Cursors.WaitCursor;
                if (Interval == null || !Interval.Contains(value))
                {
                    var interval = Intervals.FirstOrDefault(p => p.Contains(value));
                    if (interval == null)
                    {
                        var info = engine.GetOverlayInfo(value);
                        interval = new FrameInterval
                        {
                            Frames = { info },
                            Modified = true
                        };
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
            nudAngle.Value = info.Angle;
            nudOverlayWidth.Value = info.Width;
            nudOverlayHeight.Value = info.Height;
            nudCropLeft.Value = info.CropLeft;
            nudCropTop.Value = info.CropTop;
            nudCropRight.Value = info.CropRight;
            nudCropBottom.Value = info.CropBottom;
            chbOverlaySizeSync.Checked = Round(engine.OverInfo.Width / info.AspectRatio) == engine.OverInfo.Height;

            update = false;
        }

        private static int Round(double value) => (int)Math.Round(value);

        #region Form behavior
        public OverlayEditor(OverlayEngine engine, ScriptEnvironment env)
        {
            this.engine = engine;
            this.env = env;
            InitializeComponent();
            cbMode.Items.AddRange(Enum.GetValues(typeof(OverlayMode)).Cast<object>().ToArray());
            cbMode.SelectedItem = OverlayMode.Fit;
            nudOutputWidth.Value = engine.SrcInfo.Width;
            nudOutputHeight.Value = engine.SrcInfo.Height;
            nudCurrentFrame.Maximum = trackBar.Maximum = engine.GetVideoInfo().num_frames;
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
                    cbMode.SelectedItem = cbMode.SelectedItem.Equals(OverlayMode.Difference)
                        ? OverlayMode.Fit
                        : OverlayMode.Difference;
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
                default:
                    e.Handled = false;
                    break;
            }
        }

        private void LoadStat()
        {
            Cursor.Current = Cursors.WaitCursor;
            Intervals.RaiseListChangedEvents = false;
            Intervals.Clear();
            if (engine?.OverlayStat == null) return;
            FrameInterval last = null;
            foreach (var info in engine.OverlayStat.Frames)
            {
                if (info.FrameNumber == engine.GetVideoInfo().num_frames)
                    break;
                if (last == null || !last.Frames.First().Equals(info))
                    last = Intervals.AddNew();
                last.Frames.Add(info);
            }

            Text = Text + " " + Intervals.Count;
            Intervals.RaiseListChangedEvents = true;
            Interval = Intervals.FirstOrDefault(p => p.Contains(CurrentFrame));
            Intervals.ResetBindings();
            UpdateControls(Interval);
            grid.Refresh();
            Cursor.Current = Cursors.Default;
        }

        private void OverlayEditorForm_Load(object sender, EventArgs e)
        {
            LoadStat();
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
                nudOverlayHeight.Value = Round((double)nudOverlayWidth.Value / FrameInfo.AspectRatio);
                nudOverlayHeight.ValueChanged += Render;
            }
            else if (sender == nudOverlayHeight && chbOverlaySizeSync.Checked)
            {
                nudOverlayWidth.ValueChanged -= Render;
                nudOverlayWidth.Value = Round((double)nudOverlayHeight.Value * FrameInfo.AspectRatio);
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
            return new OverlayInfo
            {
                FrameNumber = CurrentFrame,
                X = (int) nudX.Value,
                Y = (int) nudY.Value,
                Angle = (int) nudAngle.Value,
                Width = (int) nudOverlayWidth.Value,
                Height = (int) nudOverlayHeight.Value,
                CropLeft = (int) nudCropLeft.Value,
                CropTop = (int) nudCropTop.Value,
                CropRight = (int) nudCropRight.Value,
                CropBottom = (int) nudCropBottom.Value,
                Diff = FrameInfo?.Diff ?? -1
            };
        }

        private void RenderImpl()
        {
            using (new DynamicEnviroment(env))
            using (new VideoFrameCollector())
            {
                var info = GetOverlayInfo();
                var outSize = new Size((int) nudOutputWidth.Value, (int) nudOutputHeight.Value);
                var crop = info.GetCrop();
                using (var src = engine.SrcClip.Dynamic().ConvertToRGB24(matrix: "Rec709"))
                using (var over = engine.OverClip.Dynamic().ConvertToRGB24(matrix: "Rec709"))
                using (var srcMask = engine.SrcMaskClip?.Dynamic().ConvertToRGB24(matrix: "Rec709"))
                using (var overMask = engine.OverMaskClip ?.Dynamic().ConvertToRGB24(matrix: "Rec709"))
                {
                    VideoFrame frame;
                    if (chbPreview.Checked)
                    {
                        frame = src.StaticOverlayRender(over,
                            info.X, info.Y, info.Angle / 100.0, info.Width, info.Height,
                            crop.Left, crop.Top, crop.Right, crop.Bottom, info.Diff,
                            sourceMask: srcMask, overlayMask: overMask,
                            lumaOnly: false, outWidth: outSize.Width, outHeight: outSize.Height,
                            gradient: (int) nudGradientSize.Value, noise: (int) nudNoiseSize.Value,
                            dynamicNoise: true, mode: (int) cbMode.SelectedItem, opacity: (double)nudOpacity.Value/100.0,
                            debug: chbDebug.Checked)[CurrentFrame];
                    }
                    else frame = src.BilinearResize(outSize.Width, outSize.Height)[CurrentFrame];
                    var wrapper = new Bitmap(outSize.Width, outSize.Height, frame.GetPitch(),
                        PixelFormat.Format24bppRgb, frame.GetReadPtr());
                    var res = new Bitmap(wrapper);
                    res.RotateFlip(RotateFlipType.Rotate180FlipX);
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
            if (e.RowIndex < 1) return;
            var item = Intervals[e.RowIndex];
            if (item.Modified)
                grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
            else if (item.Diff > engine.MaxDiff)
                grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightCoral;
            else if (item.Frames.Any(p => p.Diff > engine.MaxDiff))
                grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightPink;
        }

        private void ResetCurrent(object sender = null, EventArgs e = null)
        {
            var info = engine.OverlayStat[CurrentFrame];
            UpdateControls(info);
            RenderImpl();
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            if (NeedSave)
            {
                var answer = MessageBox.Show("Save changed items before reset?", "Warning",
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
            using (new DynamicEnviroment(env))
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
            CurrentFrame = (int)nudCurrentFrame.Value;
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
            if (Interval == null) return;
            Cursor.Current = Cursors.WaitCursor;
            Intervals.RaiseListChangedEvents = false;
            var lastInterval = Interval;
            var text = Text;
            var first = Interval.First;
            var length = Interval.Length;
            for (int frame = Interval.First, last = Interval.Last; frame <= last; frame++)
            {
                Text = $"{text}: {frame - first + 1}/{length}";
                Application.DoEvents();
                using (new DynamicEnviroment(env))
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
            Text = text;
            Interval = Intervals.First(p => p.Contains(CurrentFrame));
            Intervals.RaiseListChangedEvents = true;
            Intervals.ResetBindings();
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
            Cursor.Current = Cursors.WaitCursor;
            Intervals.RaiseListChangedEvents = false;
            CheckChanges(Interval);
            var lastInterval = Interval;
            var text = Text;
            var first = Interval.First;
            var length = Interval.Length;
            for (int frame = Interval.First, last = Interval.Last; frame <= last; frame++)
            {
                Text = $"{text}: {frame - first + 1}/{length}";
                Application.DoEvents();
                using (new DynamicEnviroment(env))
                using (new VideoFrameCollector())
                {
                    var configs = engine.LoadConfigs();
                    foreach (var config in configs)
                    {
                        var delta = (int) nudDistance.Value;
                        config.MinX = Math.Max(config.MinX, lastInterval.X - delta);
                        config.MaxX = Math.Min(config.MaxX, lastInterval.X + delta);
                        config.MinY = Math.Max(config.MinY, lastInterval.Y - delta);
                        config.MaxY = Math.Min(config.MaxY, lastInterval.Y + delta);
                        config.Angle1 = config.Angle2 = lastInterval.Angle / 100.0; //TODO fix
                        var rect = lastInterval.GetRectangle(engine.OverInfo.Size);
                        var ar = rect.Width / rect.Height;
                        if (!config.FixedAspectRatio) //TODO fix
                        {
                            config.AspectRatio1 = ar * 0.999;
                            config.AspectRatio2 = ar * 1.001;
                        }
                        var scale = (double) nudScale.Value / 1000;
                        config.MinArea = Math.Max(config.MinArea, (int) (lastInterval.Area * (1 - scale)));
                        config.MaxArea = Math.Min(config.MaxArea, (int) Math.Round(lastInterval.Area * (1 + scale)));
                    }
                    var info = engine.AutoOverlayImpl(frame, configs);
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
            Text = text;
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
            using (new DynamicEnviroment(env))
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

        private void btnPanScanFull_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) ==
                DialogResult.No) return;
            if (Interval == null) return;
            Cursor.Current = Cursors.WaitCursor;
            Intervals.RaiseListChangedEvents = false;
            CheckChanges(Interval);
            var text = Text;
            var intervals = Intervals.Where(p => p.Length >= engine.BackwardFrameCount).ToArray();
            var length = intervals.Sum(p => p.Length);
            var total = 0;
            foreach (var interval in intervals)
            {
                var lastInterval = interval;
                
                for (int frame = interval.First, last = interval.Last; frame <= last; frame++)
                {
                    Text = $"{text}: {++total}/{length} {(total*100.0)/length:F2}%";
                    Application.DoEvents();
                    using (new DynamicEnviroment(env))
                    using (new VideoFrameCollector())
                    {
                        var configs = engine.LoadConfigs();
                        foreach (var config in configs)
                        {
                            var delta = (int) nudDistance.Value;
                            config.MinX = Math.Max(config.MinX, lastInterval.X - delta);
                            config.MaxX = Math.Min(config.MaxX, lastInterval.X + delta);
                            config.MinY = Math.Max(config.MinY, lastInterval.Y - delta);
                            config.MaxY = Math.Min(config.MaxY, lastInterval.Y + delta);
                            config.Angle1 = config.Angle2 = lastInterval.Angle / 100.0; //TODO fix
                            var rect = lastInterval.GetRectangle(engine.OverInfo.Size);
                            var ar = rect.Width / rect.Height;
                            if (!config.FixedAspectRatio) //TODO fix
                            {
                                config.AspectRatio1 = ar * 0.999;
                                config.AspectRatio2 = ar * 1.001;
                            }

                            var scale = (double) nudScale.Value / 1000;
                            config.MinArea = Math.Max(config.MinArea, (int) (lastInterval.Area * (1 - scale)));
                            config.MaxArea = Math.Min(config.MaxArea,
                                (int) Math.Round(lastInterval.Area * (1 + scale)));
                        }

                        var info = engine.AutoOverlayImpl(frame, configs);
                        info.FrameNumber = frame;
                        if (frame == CurrentFrame)
                            UpdateControls(info);
                        if (!info.Equals(lastInterval))
                        {
                            var newInterval = new FrameInterval
                            {
                                Frames = {info},
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
            }

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
    }
}
