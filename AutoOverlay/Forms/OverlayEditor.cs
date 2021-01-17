using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AutoOverlay.AviSynth;
using AutoOverlay.Overlay;
using AvsFilterNet;

namespace AutoOverlay.Forms
{
    public partial class OverlayEditor : Form
    {
        #region Fields and properties
        private readonly KeyboardHook keyboardHook = new KeyboardHook(true);

        private bool captured;

        private int _currentFrame = -1;

        private string compareFilename;

        private bool update;

        private readonly SynchronizationContext context;

        private volatile FrameInterval prevInterval;

        private volatile object sync;

        private volatile string activeOperationId;

        public OverlayEngine Engine { get; }

        public BindingList<FrameInterval> Intervals { get; } = new BindingList<FrameInterval>();

        public ScriptEnvironment Env { get; }

        public double MaxDeviation => (double) nudDeviation.Value / 100.0;

        private bool NeedSave => Intervals.Any(p => p.Modified);

        private static int Round(double value) => (int) Math.Round(value);
        #endregion

        #region State
        public int CurrentFrame
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
                if (_currentFrame >= 0)
                    CheckChanges();
                nudCurrentFrame.Value = trackBar.Value = _currentFrame = value;
                if (Interval == null || !Interval.Contains(value))
                {
                    var interval = Intervals.FirstOrDefault(p => p.Contains(value));
                    if (interval == null)
                    {
                        using (new DynamicEnvironment(Env))
                        {
                            var info = Engine.GetOverlayInfo(value);
                            interval = new FrameInterval(info);
                        }

                        InsertInterval(interval);
                    }

                    Interval = interval;
                }
                UpdateControls(Interval[CurrentFrame]);
                RenderImpl();
            }
        }
        public FrameInterval Interval
        {
            get => grid.BindingContext[Intervals].Position >= 0 ? grid?.BindingContext?[Intervals].Current as FrameInterval : null;
            set => grid.BindingContext[Intervals].Position = value == null ? -1 : Intervals.IndexOf(value);
        }

        private OverlayInfo CurrentFrameInfo => Interval?[CurrentFrame];

        public void CheckChanges()
        {
            var interval = prevInterval;
            if (interval == null || !interval.Contains(CurrentFrame)) return;
            var info = GetOverlayInfo();
            if (!info.Equals(interval[CurrentFrame]))
            {
                if (interval.Fixed)
                    foreach (var frame in interval)
                    {
                        frame.CopyFrom(info);
                        frame.Modified = true;
                    }
                else
                {
                    interval[CurrentFrame].CopyFrom(info);
                    interval[CurrentFrame].Modified = true;
                }
                grid.Refresh();
            }
        }
        #endregion

        #region Form behavior
        public OverlayEditor(OverlayEngine engine, ScriptEnvironment env, SynchronizationContext context)
        {
            this.context = context;
            Engine = engine;
            Env = env;
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
            nudDeviation.Value = engine.Stabilize ? 0 : new decimal(engine.MaxDeviation * 100.0);
            nudMaxFrame.Value = nudMinFrame.Maximum = nudMaxFrame.Maximum = engine.GetVideoInfo().num_frames - 1;
            engine.CurrentFrameChanged += OnCurrentFrameChanged;
            keyboardHook.KeyDown += keyboardHook_KeyDown;
            Closing += (o, e) => keyboardHook.Dispose();
        }

        public void UpdateControls(OverlayInfo info)
        {
            if (!(panelManage.Enabled = info != null))
                return;

            update = true;
            Interval?.ClearCache();

            nudX.Value = info.X;
            nudY.Value = info.Y;
            nudAngle.Value = (decimal)(info.Angle / 100.0);
            nudOverlayWidth.Value = info.Width;
            nudOverlayHeight.Value = info.Height;
            var crop = info.GetCrop();
            nudCropLeft.Value = (decimal)crop.Left;
            nudCropTop.Value = (decimal)crop.Top;
            nudCropRight.Value = (decimal)crop.Right;
            nudCropBottom.Value = (decimal)crop.Bottom;
            tbWarp.Text = info.Warp.ToString();
            chbOverlaySizeSync.Checked = Round(Engine.OverInfo.Width / info.GetAspectRatio(Engine.OverInfo.Size)) == Engine.OverInfo.Height;

            update = false;
        }

        private void grid_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            var compareLimit = (double)nudCompare.Value;
            if (e.RowIndex < 0) return;
            var item = Intervals[e.RowIndex];
            if (item.Modified)
                grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
            else if (item.Comparison < compareLimit)
                grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.SkyBlue;
            else if (item.Diff > Engine.MaxDiff)
                grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightCoral;
            else if (item.Any(p => p.Diff > Engine.MaxDiff))
                grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightPink;
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

        private void keyboardHook_KeyDown(object sender, KeyEventArgs e)
        {
            if (ActiveForm != this) return;
            e.Handled = true;
            switch (e.KeyData)
            {
                case Keys.Enter:
                    if (ActiveControl is NumericUpDown nud)
                    {
                        var val = decimal.Parse(nud.Text);
                        if (val != nud.Value)
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

        private void OverlayEditorForm_Load(object sender, EventArgs e)
        {
            LoadStat(true);
            grid.DataSource = Intervals;
            grid.BindingContext[Intervals].CurrentChanged += UpdateInterval;
        }

        private void OverlayEditorForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Engine.CurrentFrameChanged -= OnCurrentFrameChanged;
            pictureBox.Image?.Dispose();
        }

        private void OnCurrentFrameChanged(object sender, FrameEventArgs args)
        {
            BeginInvoke((Action)(() => CurrentFrame = args.FrameNumber));
        }

        private void grid_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            var interval = Intervals[e.RowIndex];
            var fieldName = grid.Columns[e.ColumnIndex].Name;
            e.Value = interval.GetType().GetProperty(fieldName).GetValue(interval);
        }

        private void chbEditor_CheckedChanged(object sender, EventArgs e)
        {
            panelManage.Visible = chbEditor.Checked;
            pictureBox.SizeMode = chbEditor.Checked ? PictureBoxSizeMode.Zoom : PictureBoxSizeMode.CenterImage;
        }

        private void SuppressKeyPress(object sender, KeyEventArgs e)
        {
            e.Handled = true;
        }

        private void btnResetCrop_Click(object sender, EventArgs e)
        {
            nudCropLeft.Value = nudCropTop.Value = nudCropRight.Value = nudCropBottom.Value = 0;
        }
        #endregion

        #region Stat
        private void LoadStat(bool initial = false)
        {
            Cursor.Current = Cursors.WaitCursor;
            Intervals.RaiseListChangedEvents = false;
            Intervals.Clear();
            if (Engine?.OverlayStat == null) return;
            FrameInterval lastInterval = null;
            OverlayInfo lastFrame = null;
            var min = (int)nudMinFrame.Value;
            var max = (int)nudMaxFrame.Value;
            var overSize = Engine.OverInfo.Size;
            var compareLimit = (double)nudCompare.Value;
            using (var refStat = compareFilename == null ? null : new FileOverlayStat(compareFilename, Engine.SrcInfo.Size, Engine.OverInfo.Size))
                foreach (var info in Engine.OverlayStat.Frames.Where(p => p.FrameNumber >= min && p.FrameNumber <= max))
                {
                    info.Comparison = refStat?[info.FrameNumber]?.Compare(info, Engine.SrcInfo.Size) ?? 2;
                    var compareFailed = info.Comparison < compareLimit;
                    var diffFailed = info.Diff > Engine.MaxDiff;
                    if (chbDefective.Checked && !diffFailed && !compareFailed)
                        continue;
                    if (lastInterval == null
                        || lastFrame.FrameNumber != info.FrameNumber - 1
                        || !lastFrame.NearlyEquals(info, overSize, MaxDeviation)
                        || Engine.KeyFrames.Contains(info.FrameNumber))
                    {
                        if (initial && Intervals.Count == 2000)
                        {
                            nudMaxFrame.ValueChanged -= Reset;
                            nudMaxFrame.Value = lastInterval.Last;
                            nudMaxFrame.ValueChanged += Reset;
                            break;
                        }
                        lastInterval = new FrameInterval(info);
                        Intervals.Add(lastInterval);

                    }
                    lastInterval.Add(info);
                    lastFrame = info;
                }
            Intervals.RaiseListChangedEvents = true;
            Interval = Intervals.FirstOrDefault(p => p.Contains(CurrentFrame));
            Intervals.ResetBindings();
            nudCurrentFrame.ValueChanged -= nudCurrentFrame_ValueChanged;
            nudCurrentFrame.Minimum = trackBar.Minimum = (int)nudMinFrame.Value;
            nudCurrentFrame.Maximum = trackBar.Maximum = (int)nudMaxFrame.Value;
            _currentFrame = -1;
            CurrentFrame = (int)nudCurrentFrame.Value;
            Interval = Intervals.FirstOrDefault(p => p.Contains(CurrentFrame));
            nudCurrentFrame.ValueChanged += nudCurrentFrame_ValueChanged;

            UpdateControls(Interval?[CurrentFrame]);
            grid.Refresh();
            Cursor.Current = Cursors.Default;
        }

        private void SaveStat(object sender = null, EventArgs e = null)
        {
            var watch = new Stopwatch();
            watch.Start();
            Cursor.Current = Cursors.WaitCursor;
            CheckChanges();
            watch.Stop();
            Debug.WriteLine($"Check changes: {watch.ElapsedMilliseconds}");
            watch.Restart();
            var frames = Intervals.Where(p => p.Modified).SelectMany(p => p).ToArray();
            Engine.OverlayStat.Save(frames);
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

        private void btnCompare_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog(this) != DialogResult.OK) return;
            compareFilename = openFileDialog1.FileName;
            Compare();
        }

        private void Compare(object sender = null, EventArgs e = null)
        {
            if (compareFilename == null) return;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                using (var refStat = new FileOverlayStat(compareFilename, Engine.SrcInfo.Size, Engine.OverInfo.Size))
                    foreach (var frame in Intervals.SelectMany(p => p))
                    {
                        var refFrame = refStat[frame.FrameNumber];
                        if (refFrame == null) continue;
                        frame.Comparison = refFrame.Compare(frame, Engine.OverInfo.Size);
                    }
                grid.Refresh();
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }
        #endregion

        #region Frame management
        private void Render(object sender = null, EventArgs e = null)
        {
            if (update || CurrentFrameInfo == null) return;
            if ((sender == chbOverlaySizeSync || sender == nudOverlayWidth) && chbOverlaySizeSync.Checked)
            {
                nudOverlayHeight.ValueChanged -= Render;
                nudOverlayHeight.Value = Round((double)nudOverlayWidth.Value / CurrentFrameInfo.GetAspectRatio(Engine.OverInfo.Size));
                nudOverlayHeight.ValueChanged += Render;
            }
            else if (sender == nudOverlayHeight && chbOverlaySizeSync.Checked)
            {
                nudOverlayWidth.ValueChanged -= Render;
                nudOverlayWidth.Value = Round((double)nudOverlayHeight.Value * CurrentFrameInfo.GetAspectRatio(Engine.OverInfo.Size));
                nudOverlayWidth.ValueChanged += Render;
            }

            RenderImpl();
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
                Warp = Warp.Parse(tbWarp.Text),
                Width = (int) nudOverlayWidth.Value,
                Height = (int) nudOverlayHeight.Value,
                CropLeft = crop(nudCropLeft.Value),
                CropTop = crop(nudCropTop.Value),
                CropRight = crop(nudCropRight.Value),
                CropBottom = crop(nudCropBottom.Value),
                SourceWidth = CurrentFrameInfo?.SourceWidth ?? Engine.SrcInfo.Width,
                SourceHeight = CurrentFrameInfo?.SourceHeight ?? Engine.SrcInfo.Height,
                BaseWidth = CurrentFrameInfo?.BaseWidth ?? Engine.OverInfo.Width,
                BaseHeight = CurrentFrameInfo?.BaseHeight ?? Engine.OverInfo.Height,
                Diff = CurrentFrameInfo?.Diff ?? -1
            };
        }

        public void RenderImpl()
        {
            var request = new RenderRequest
            {
                info = GetOverlayInfo(),
                env = Env,
                outSize = new Size((int) nudOutputWidth.Value, (int) nudOutputHeight.Value),
                preview = chbPreview.Checked,
                engine = Engine,
                pictureBox = pictureBox,
                rgb = chbRGB.Checked,
                matrix = cbMatrix.Enabled
                    ? cbMatrix.SelectedIndex > 0 ? cbMatrix.SelectedItem.ToString() : string.Empty
                    : null,
                gradient = (int) nudGradientSize.Value,
                noise = (int) nudNoiseSize.Value,
                mode = (int) cbMode.SelectedItem,
                overlayMode = cbOverlayMode.SelectedItem.ToString(),
                opacity = tbOpacity.Value / 100.0,
                colorAdjust = chbColorAdjust.Checked ? tbColorAdjust.Value / 100.0 : -1,
                debug = chbDebug.Checked
            };
            Post(request, RenderInternal, (_, image) =>
            {
                pictureBox.SuspendLayout();
                pictureBox.Image?.Dispose();
                pictureBox.Image = image;
                pictureBox.ResumeLayout();
            });
        }

        class RenderRequest
        {
            public OverlayInfo info;
            public ScriptEnvironment env;
            public Size outSize;
            public bool preview;
            public OverlayEngine engine;
            public PictureBox pictureBox;
            public bool rgb;
            public string matrix;
            public int gradient, noise;
            public int mode;
            public string overlayMode;
            public double opacity;
            public double colorAdjust;
            public bool debug;
        }

        private static Bitmap RenderInternal(RenderRequest request)
        {
            var info = request.info;
            using dynamic invoker = new DynamicEnvironment(request.env);
            using var collector = new VideoFrameCollector();
            var engine = request.engine;
            var crop = info.GetCrop();
            var outClip = request.preview
                        ? invoker.StaticOverlayRender(
                            engine.Source, engine.Overlay,
                            info.X, info.Y, info.Angle / 100.0,
                            info.Width, info.Height,
                            crop.Left, crop.Top, crop.Right, crop.Bottom,
                            warpPoints: info.Warp.ToString(),
                            diff: info.Diff,
                            sourceMask: engine.SourceMask,
                            overlayMask: engine.OverlayMask,
                            width: request.outSize.Width,
                            height: request.outSize.Height,
                            gradient: request.gradient,
                            noise: request.noise,
                            dynamicNoise: true,
                            mode: request.mode,
                            overlayMode: request.overlayMode,
                            opacity: request.opacity,
                            colorAdjust: request.colorAdjust,
                            matrix: (request.rgb ? request.matrix : null) ?? string.Empty,
                            upsize: engine.Resize,
                            downsize: engine.Resize,
                            rotate: engine.Rotate,
                            debug: request.debug,
                            invert: false)
                        : engine.ResizeRotate(engine.Source, 
                            engine.Resize, engine.Rotate, 
                            request.outSize.Width, request.outSize.Height);
            if (request.matrix != null)
            {
                outClip = string.Empty.Equals(request.matrix)
                    ? outClip.ConvertToRGB24()
                    : outClip.ConvertToRGB24(matrix: request.matrix);
            }
            VideoFrame frame = outClip[info.FrameNumber];
            return new Bitmap(frame.ToBitmap(PixelFormat.Format24bppRgb));
        }
        #endregion

        #region Scene management
        private void btnFix_Click(object sender, EventArgs e)
        {
            if (CurrentFrameInfo != null)
                Interval.CopyFrom(GetOverlayInfo());
            grid.Refresh();
        }

        private void InsertInterval(FrameInterval interval)
        {
            var index = Intervals.Count(p => p.First < interval.First);
            Intervals.Insert(index, interval);
        }

        private void UpdateInterval(object sender = null, EventArgs e = null)
        {
            if (Interval != null && !Interval.Contains(CurrentFrame))
                CurrentFrame = Interval.First;
            prevInterval = Interval;
        }

        private void btnSeparate_Click(object sender, EventArgs e)
        {
            if (CurrentFrameInfo == null || Interval.Length <= 1) return;
            CheckChanges();
            var parent = Interval;
            Intervals.RaiseListChangedEvents = false;
            var current = new FrameInterval(CurrentFrameInfo)
            {
                Modified = true
            };
            Intervals.Remove(parent);
            if (CurrentFrame != parent.First)
            {
                var prev = new FrameInterval(parent.Where(p => p.FrameNumber < CurrentFrame))
                {
                    Modified = true
                };
                InsertInterval(prev);
            }
            InsertInterval(current);
            if (CurrentFrame != parent.Last)
            {
                var next = new FrameInterval(parent.Where(p => p.FrameNumber > CurrentFrame))
                {
                    Modified = true
                };
                InsertInterval(next);
            }
            Intervals.RaiseListChangedEvents = true;
            Intervals.ResetBindings();
            Interval = current;
            grid.Refresh();
        }

        private void ResetCurrent(object sender = null, EventArgs e = null)
        {
            var info = Engine.OverlayStat[CurrentFrame];
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
            Interval.AddRange(prev);
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
            Interval.AddRange(next);
        }

        private void btnJoinTo_Click(object sender, EventArgs e)
        {
            if (Interval == null) return;
            var nud = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = Engine.GetVideoInfo().num_frames - 1,
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

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (Interval == null || MessageBox.Show(
                "Selected frames will be deleted. Continue?", "Warning",
                MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.No) return;
            for (var frame = Interval.First; frame <= Interval.Last; frame++)
                Engine.OverlayStat[frame] = null;
            Intervals.Remove(Interval);
            grid.Refresh();
        }
        #endregion

        #region Frame position manamegment
        private void nudCurrentFrame_ValueChanged(object sender, EventArgs e)
        {
            CurrentFrame = (int)nudCurrentFrame.Value;
        }

        private void nudCurrentFrame_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                nudCurrentFrame.Value = int.Parse(nudCurrentFrame.Text);
        }

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

        #region Autoalign
        private void btnAutoOverlaySingleFrame_Click(object sender, EventArgs e)
        {
            if (Interval == null) return;
            Post(CurrentFrame, frame => Engine.AutoOverlayImpl(frame), (frame, info) =>
            {
                Interval[frame].Diff = info.Diff;
                UpdateControls(info);
                RenderImpl();
            });
        }


        private void btnAutoOverlayScene_Click(object sender, EventArgs e)
        {
            new ProgressDialog(this, new[] { Interval }, (frame, interval) => Engine.AutoOverlayImpl(frame)).ShowDialog(this);
        }

        private void btnAutoOverlaySeparatedFrame_Click(object sender, EventArgs e)
        {
            if (Interval == null) return;
            Post(CurrentFrame, frame => Engine.AutoOverlayImpl(frame), (frame, info) =>
            {
                Interval[CurrentFrame].Diff = info.Diff;
                if (Interval.Contains(CurrentFrame - 1) && !info.NearlyEquals(Interval[CurrentFrame - 1], Engine.OverInfo.Size, MaxDeviation))
                {
                    btnSeparate_Click(sender, e);
                    UpdateControls(info);
                }
                RenderImpl();
            });
        }
        #endregion

        #region Scan/Adjust
        private void btnAdjustFrame_Click(object sender, EventArgs e)
        {
            AdjustOrScanOne(GetOverlayInfo);
        }

        private void btnAdjust_Click(object sender, EventArgs e)
        {
            Adjust(new[] { Interval }, $"Adjust scene {Interval.Interval}");
        }

        private void btnAdjustClip_Click(object sender, EventArgs e)
        {
            Adjust(Intervals, "Adjust whole clip");
        }

        private void btnScanFrame_Click(object sender, EventArgs e)
        {
            AdjustOrScanOne(() => Interval?[CurrentFrame - 1] ?? GetOverlayInfo());
        }

        private void brnPanScan_Click(object sender, EventArgs e)
        {
            if (Interval == null) return;
            PanScan(new List<FrameInterval> { Interval }, $"Scan scene {Interval.Interval}");
        }

        private void btnPanScanFull_Click(object sender, EventArgs e)
        {
            PanScan(Intervals.Where(p => p.Length >= Engine.BackwardFrames).ToList(), "Scan whole clip");
        }

        private void AdjustOrScanOne(Func<OverlayInfo> keyFrame)
        {
            if (Interval == null) return;
            Post(new
                {
                    Frame = CurrentFrame,
                    KeyInfo = keyFrame(),
                    Delta = (int)nudDistance.Value,
                    Scale = (double)nudScale.Value / 1000
                },
                tuple => Engine.PanScanImpl(tuple.KeyInfo, tuple.Frame, tuple.Delta, tuple.Scale, false),
                (tuple, info) =>
                {
                    Interval[tuple.Frame].Diff = info.Diff;
                    UpdateControls(info);
                    RenderImpl();
                });
        }

        private void Adjust(ICollection<FrameInterval> intervals, string operationName)
        {
            var currentFrame = CurrentFrame;
            new ProgressDialog(this, intervals, (frame, interval) =>
            {
                var delta = (int) nudDistance.Value;
                var scale = (double) nudScale.Value / 1000;
                var keyFrame = interval == Interval ? Interval[currentFrame] : interval.First();
                return Engine.PanScanImpl(keyFrame, frame, delta, scale, false);
            })
            {
                Text = operationName
            }.ShowDialog(this);
        }

        private void PanScan(ICollection<FrameInterval> intervals, string operationName)
        {
            var currentFrame = CurrentFrame;
            new ProgressDialog(this, intervals, (frame, interval) =>
            {
                var delta = (int) nudDistance.Value;
                var scale = (double) nudScale.Value / 1000;
                var keyFrame = interval[frame - 1] ?? interval[frame];
                if (interval == Interval)
                    keyFrame.Warp = Interval[currentFrame].Warp;
                return Engine.PanScanImpl(keyFrame, frame, delta, scale, false);
            })
            {
                Text = operationName
            }.ShowDialog(this);
        }
        #endregion

        #region Multithreading
        public void Post<T, V>(T param, Func<T, V> evaluator, Action<T, V> callback)
        {
            Post(() => evaluator(param), res => callback(param, res));
        }

        public void Post<T>(T param, Action<T> action)
        {
            Post<object>(() =>
            {
                action(param);
                return null;
            }, p => { });
        }

        public void Post<V>(Func<V> evaluator, Action<V> callback)
        {
            Cursor = Cursors.WaitCursor;
            activeOperationId = Guid.NewGuid().ToString();
            context.Post(operationId =>
            {
                try
                {
                    if (!operationId.Equals(this.SafeInvoke(p => activeOperationId)))
                        return;
                    var res = evaluator.Invoke();
                    if (callback != null)
                        this.SafeInvoke(p =>
                        {
                            if (operationId.Equals(activeOperationId))
                            {
                                callback.Invoke(res);
                            }
                        });
                }
                finally
                {
                    this.SafeInvoke(p => p.Cursor = Cursors.Default);
                }
            }, activeOperationId);
        }

        #endregion
    }
}
