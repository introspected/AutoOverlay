namespace AutoOverlay.Forms
{
    partial class OverlayEditor
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OverlayEditor));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.panel1 = new System.Windows.Forms.Panel();
            this.trackBar = new System.Windows.Forms.TrackBar();
            this.panel3 = new System.Windows.Forms.Panel();
            this.chbDefective = new System.Windows.Forms.CheckBox();
            this.nudMaxDiff = new System.Windows.Forms.NumericUpDown();
            this.nudMinSceneLength = new System.Windows.Forms.NumericUpDown();
            this.nudMinFrame = new System.Windows.Forms.NumericUpDown();
            this.nudMaxFrame = new System.Windows.Forms.NumericUpDown();
            this.label31 = new System.Windows.Forms.Label();
            this.label30 = new System.Windows.Forms.Label();
            this.label21 = new System.Windows.Forms.Label();
            this.label19 = new System.Windows.Forms.Label();
            this.nudCurrentFrame = new System.Windows.Forms.NumericUpDown();
            this.chbEditor = new System.Windows.Forms.CheckBox();
            this.label20 = new System.Windows.Forms.Label();
            this.panelManage = new System.Windows.Forms.Panel();
            this.grid = new System.Windows.Forms.DataGridView();
            this.Resolution = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Fixed = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.panel2 = new System.Windows.Forms.Panel();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.gbScan = new System.Windows.Forms.GroupBox();
            this.btnScanClip = new System.Windows.Forms.Button();
            this.btnScanScene = new System.Windows.Forms.Button();
            this.btnScanFrame = new System.Windows.Forms.Button();
            this.gbAlign = new System.Windows.Forms.GroupBox();
            this.btnAlignScene = new System.Windows.Forms.Button();
            this.btnAlignSingle = new System.Windows.Forms.Button();
            this.btnAlignFrame = new System.Windows.Forms.Button();
            this.gbAdjust = new System.Windows.Forms.GroupBox();
            this.btnAdjustClip = new System.Windows.Forms.Button();
            this.btnAdjustScene = new System.Windows.Forms.Button();
            this.btnAdjustFrame = new System.Windows.Forms.Button();
            this.label18 = new System.Windows.Forms.Label();
            this.label17 = new System.Windows.Forms.Label();
            this.nudScale = new System.Windows.Forms.NumericUpDown();
            this.nudDistance = new System.Windows.Forms.NumericUpDown();
            this.nudDeviation = new System.Windows.Forms.NumericUpDown();
            this.label27 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label25 = new System.Windows.Forms.Label();
            this.chbColorAdjust = new System.Windows.Forms.CheckBox();
            this.chbRGB = new System.Windows.Forms.CheckBox();
            this.tbColorAdjust = new System.Windows.Forms.TrackBar();
            this.tbOpacity = new System.Windows.Forms.TrackBar();
            this.chbDebug = new System.Windows.Forms.CheckBox();
            this.chbPreview = new System.Windows.Forms.CheckBox();
            this.cbMatrix = new System.Windows.Forms.ComboBox();
            this.cbOverlayMode = new System.Windows.Forms.ComboBox();
            this.cbMode = new System.Windows.Forms.ComboBox();
            this.label23 = new System.Windows.Forms.Label();
            this.label24 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.nudNoiseSize = new System.Windows.Forms.NumericUpDown();
            this.nudGradientSize = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.nudOutputWidth = new System.Windows.Forms.NumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            this.nudOutputHeight = new System.Windows.Forms.NumericUpDown();
            this.label26 = new System.Windows.Forms.Label();
            this.label22 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnFix = new System.Windows.Forms.Button();
            this.tbWarp = new System.Windows.Forms.TextBox();
            this.label29 = new System.Windows.Forms.Label();
            this.nudCompare = new System.Windows.Forms.NumericUpDown();
            this.btnCompare = new System.Windows.Forms.Button();
            this.nudX = new System.Windows.Forms.NumericUpDown();
            this.label8 = new System.Windows.Forms.Label();
            this.nudY = new System.Windows.Forms.NumericUpDown();
            this.chbOverlaySizeSync = new System.Windows.Forms.CheckBox();
            this.nudAngle = new System.Windows.Forms.NumericUpDown();
            this.nudOverlayHeight = new System.Windows.Forms.NumericUpDown();
            this.label9 = new System.Windows.Forms.Label();
            this.nudOverlayWidth = new System.Windows.Forms.NumericUpDown();
            this.label14 = new System.Windows.Forms.Label();
            this.label28 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnSave = new System.Windows.Forms.ToolStripButton();
            this.btnReset = new System.Windows.Forms.ToolStripButton();
            this.btnResetCurrent = new System.Windows.Forms.ToolStripButton();
            this.btnJoinTo = new System.Windows.Forms.ToolStripButton();
            this.btnJoinNext = new System.Windows.Forms.ToolStripButton();
            this.btnJoinPrev = new System.Windows.Forms.ToolStripButton();
            this.btnSeparate = new System.Windows.Forms.ToolStripButton();
            this.btnResetInterval = new System.Windows.Forms.ToolStripButton();
            this.btnDelete = new System.Windows.Forms.ToolStripButton();
            this.pictureBox = new System.Windows.Forms.PictureBox();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.intervalDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.xDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Angle = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.diffDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.frameIntervalBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trackBar)).BeginInit();
            this.panel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudMaxDiff)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudMinSceneLength)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudMinFrame)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudMaxFrame)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudCurrentFrame)).BeginInit();
            this.panelManage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.grid)).BeginInit();
            this.panel2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.gbScan.SuspendLayout();
            this.gbAlign.SuspendLayout();
            this.gbAdjust.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudScale)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDistance)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDeviation)).BeginInit();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbColorAdjust)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbOpacity)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudNoiseSize)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudGradientSize)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudOutputWidth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudOutputHeight)).BeginInit();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudCompare)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudY)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudAngle)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudOverlayHeight)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudOverlayWidth)).BeginInit();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.frameIntervalBindingSource)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.trackBar);
            this.panel1.Controls.Add(this.panel3);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel1.Location = new System.Drawing.Point(0, 757);
            this.panel1.Margin = new System.Windows.Forms.Padding(4);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1579, 32);
            this.panel1.TabIndex = 0;
            // 
            // trackBar
            // 
            this.trackBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.trackBar.LargeChange = 500;
            this.trackBar.Location = new System.Drawing.Point(0, 0);
            this.trackBar.Margin = new System.Windows.Forms.Padding(4);
            this.trackBar.Maximum = 999999;
            this.trackBar.Name = "trackBar";
            this.trackBar.Size = new System.Drawing.Size(630, 56);
            this.trackBar.TabIndex = 0;
            this.trackBar.TickStyle = System.Windows.Forms.TickStyle.None;
            this.trackBar.Scroll += new System.EventHandler(this.trackBar_Scroll);
            this.trackBar.KeyDown += new System.Windows.Forms.KeyEventHandler(this.SuppressKeyPress);
            this.trackBar.KeyUp += new System.Windows.Forms.KeyEventHandler(this.SuppressKeyPress);
            this.trackBar.MouseDown += new System.Windows.Forms.MouseEventHandler(this.trackBar_MouseDown);
            this.trackBar.MouseUp += new System.Windows.Forms.MouseEventHandler(this.trackBar_MouseUp);
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.chbDefective);
            this.panel3.Controls.Add(this.nudMaxDiff);
            this.panel3.Controls.Add(this.nudMinSceneLength);
            this.panel3.Controls.Add(this.nudMinFrame);
            this.panel3.Controls.Add(this.nudMaxFrame);
            this.panel3.Controls.Add(this.label31);
            this.panel3.Controls.Add(this.label30);
            this.panel3.Controls.Add(this.label21);
            this.panel3.Controls.Add(this.label19);
            this.panel3.Controls.Add(this.nudCurrentFrame);
            this.panel3.Controls.Add(this.chbEditor);
            this.panel3.Controls.Add(this.label20);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Right;
            this.panel3.Location = new System.Drawing.Point(630, 0);
            this.panel3.Margin = new System.Windows.Forms.Padding(4);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(947, 30);
            this.panel3.TabIndex = 1;
            // 
            // chbDefective
            // 
            this.chbDefective.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chbDefective.AutoSize = true;
            this.chbDefective.Checked = true;
            this.chbDefective.CheckState = System.Windows.Forms.CheckState.Indeterminate;
            this.chbDefective.Location = new System.Drawing.Point(21, 4);
            this.chbDefective.Margin = new System.Windows.Forms.Padding(4);
            this.chbDefective.Name = "chbDefective";
            this.chbDefective.Size = new System.Drawing.Size(114, 20);
            this.chbDefective.TabIndex = 14;
            this.chbDefective.Text = "Defective only";
            this.chbDefective.ThreeState = true;
            this.chbDefective.UseVisualStyleBackColor = true;
            this.chbDefective.CheckStateChanged += new System.EventHandler(this.Reset);
            // 
            // nudMaxDiff
            // 
            this.nudMaxDiff.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.nudMaxDiff.DecimalPlaces = 1;
            this.nudMaxDiff.Location = new System.Drawing.Point(228, 2);
            this.nudMaxDiff.Margin = new System.Windows.Forms.Padding(4);
            this.nudMaxDiff.Maximum = new decimal(new int[] {
            9999,
            0,
            0,
            65536});
            this.nudMaxDiff.Name = "nudMaxDiff";
            this.nudMaxDiff.Size = new System.Drawing.Size(65, 22);
            this.nudMaxDiff.TabIndex = 14;
            this.nudMaxDiff.Value = new decimal(new int[] {
            9999,
            0,
            0,
            65536});
            this.nudMaxDiff.ValueChanged += new System.EventHandler(this.Reset);
            // 
            // nudMinSceneLength
            // 
            this.nudMinSceneLength.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.nudMinSceneLength.Location = new System.Drawing.Point(415, 2);
            this.nudMinSceneLength.Margin = new System.Windows.Forms.Padding(4);
            this.nudMinSceneLength.Maximum = new decimal(new int[] {
            99999,
            0,
            0,
            0});
            this.nudMinSceneLength.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.nudMinSceneLength.Name = "nudMinSceneLength";
            this.nudMinSceneLength.Size = new System.Drawing.Size(65, 22);
            this.nudMinSceneLength.TabIndex = 14;
            this.nudMinSceneLength.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.nudMinSceneLength.ValueChanged += new System.EventHandler(this.Reset);
            // 
            // nudMinFrame
            // 
            this.nudMinFrame.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.nudMinFrame.Location = new System.Drawing.Point(541, 2);
            this.nudMinFrame.Margin = new System.Windows.Forms.Padding(4);
            this.nudMinFrame.Maximum = new decimal(new int[] {
            200000,
            0,
            0,
            0});
            this.nudMinFrame.Name = "nudMinFrame";
            this.nudMinFrame.Size = new System.Drawing.Size(79, 22);
            this.nudMinFrame.TabIndex = 14;
            this.nudMinFrame.ValueChanged += new System.EventHandler(this.Reset);
            // 
            // nudMaxFrame
            // 
            this.nudMaxFrame.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.nudMaxFrame.Location = new System.Drawing.Point(639, 2);
            this.nudMaxFrame.Margin = new System.Windows.Forms.Padding(4);
            this.nudMaxFrame.Maximum = new decimal(new int[] {
            999999,
            0,
            0,
            0});
            this.nudMaxFrame.Name = "nudMaxFrame";
            this.nudMaxFrame.Size = new System.Drawing.Size(79, 22);
            this.nudMaxFrame.TabIndex = 14;
            this.nudMaxFrame.Value = new decimal(new int[] {
            200000,
            0,
            0,
            0});
            this.nudMaxFrame.ValueChanged += new System.EventHandler(this.Reset);
            // 
            // label31
            // 
            this.label31.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label31.AutoSize = true;
            this.label31.Location = new System.Drawing.Point(132, 5);
            this.label31.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label31.Name = "label31";
            this.label31.Size = new System.Drawing.Size(84, 16);
            this.label31.TabIndex = 5;
            this.label31.Text = "Defective diff";
            // 
            // label30
            // 
            this.label30.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label30.AutoSize = true;
            this.label30.Location = new System.Drawing.Point(295, 5);
            this.label30.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label30.Name = "label30";
            this.label30.Size = new System.Drawing.Size(107, 16);
            this.label30.TabIndex = 5;
            this.label30.Text = "Min scene length";
            // 
            // label21
            // 
            this.label21.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label21.AutoSize = true;
            this.label21.Location = new System.Drawing.Point(487, 5);
            this.label21.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(48, 16);
            this.label21.TabIndex = 5;
            this.label21.Text = "Range";
            // 
            // label19
            // 
            this.label19.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label19.AutoSize = true;
            this.label19.Location = new System.Drawing.Point(724, 5);
            this.label19.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(46, 16);
            this.label19.TabIndex = 5;
            this.label19.Text = "Frame";
            // 
            // nudCurrentFrame
            // 
            this.nudCurrentFrame.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.nudCurrentFrame.Location = new System.Drawing.Point(775, 2);
            this.nudCurrentFrame.Margin = new System.Windows.Forms.Padding(4);
            this.nudCurrentFrame.Maximum = new decimal(new int[] {
            999999,
            0,
            0,
            0});
            this.nudCurrentFrame.Name = "nudCurrentFrame";
            this.nudCurrentFrame.Size = new System.Drawing.Size(88, 22);
            this.nudCurrentFrame.TabIndex = 4;
            this.nudCurrentFrame.ValueChanged += new System.EventHandler(this.nudCurrentFrame_ValueChanged);
            this.nudCurrentFrame.KeyDown += new System.Windows.Forms.KeyEventHandler(this.nudCurrentFrame_KeyDown);
            // 
            // chbEditor
            // 
            this.chbEditor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chbEditor.AutoSize = true;
            this.chbEditor.Checked = true;
            this.chbEditor.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chbEditor.Location = new System.Drawing.Point(877, 4);
            this.chbEditor.Margin = new System.Windows.Forms.Padding(4);
            this.chbEditor.Name = "chbEditor";
            this.chbEditor.Size = new System.Drawing.Size(64, 20);
            this.chbEditor.TabIndex = 3;
            this.chbEditor.Text = "Editor";
            this.chbEditor.UseVisualStyleBackColor = true;
            this.chbEditor.CheckedChanged += new System.EventHandler(this.chbEditor_CheckedChanged);
            // 
            // label20
            // 
            this.label20.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label20.AutoSize = true;
            this.label20.Location = new System.Drawing.Point(623, 5);
            this.label20.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(11, 16);
            this.label20.TabIndex = 7;
            this.label20.Text = "-";
            // 
            // panelManage
            // 
            this.panelManage.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelManage.Controls.Add(this.grid);
            this.panelManage.Controls.Add(this.panel2);
            this.panelManage.Dock = System.Windows.Forms.DockStyle.Right;
            this.panelManage.Location = new System.Drawing.Point(993, 0);
            this.panelManage.Margin = new System.Windows.Forms.Padding(4);
            this.panelManage.Name = "panelManage";
            this.panelManage.Size = new System.Drawing.Size(586, 757);
            this.panelManage.TabIndex = 1;
            // 
            // grid
            // 
            this.grid.AllowUserToAddRows = false;
            this.grid.AllowUserToDeleteRows = false;
            this.grid.AllowUserToResizeRows = false;
            this.grid.AutoGenerateColumns = false;
            this.grid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
            this.grid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.grid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.intervalDataGridViewTextBoxColumn,
            this.xDataGridViewTextBoxColumn,
            this.Resolution,
            this.Angle,
            this.diffDataGridViewTextBoxColumn,
            this.Fixed});
            this.grid.DataSource = this.frameIntervalBindingSource;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.grid.DefaultCellStyle = dataGridViewCellStyle2;
            this.grid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grid.Location = new System.Drawing.Point(0, 0);
            this.grid.Margin = new System.Windows.Forms.Padding(4);
            this.grid.Name = "grid";
            this.grid.ReadOnly = true;
            this.grid.RowHeadersVisible = false;
            this.grid.RowHeadersWidth = 51;
            this.grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.grid.Size = new System.Drawing.Size(584, 163);
            this.grid.TabIndex = 1;
            this.grid.VirtualMode = true;
            this.grid.CellValueNeeded += new System.Windows.Forms.DataGridViewCellValueEventHandler(this.grid_CellValueNeeded);
            this.grid.RowPrePaint += new System.Windows.Forms.DataGridViewRowPrePaintEventHandler(this.grid_RowPrePaint);
            this.grid.KeyDown += new System.Windows.Forms.KeyEventHandler(this.SuppressKeyPress);
            this.grid.KeyUp += new System.Windows.Forms.KeyEventHandler(this.SuppressKeyPress);
            // 
            // Resolution
            // 
            this.Resolution.DataPropertyName = "Size";
            this.Resolution.HeaderText = "Size";
            this.Resolution.MinimumWidth = 6;
            this.Resolution.Name = "Resolution";
            this.Resolution.ReadOnly = true;
            this.Resolution.Width = 62;
            // 
            // Fixed
            // 
            this.Fixed.DataPropertyName = "Fixed";
            this.Fixed.HeaderText = "Fixed";
            this.Fixed.MinimumWidth = 6;
            this.Fixed.Name = "Fixed";
            this.Fixed.ReadOnly = true;
            this.Fixed.Width = 46;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.groupBox3);
            this.panel2.Controls.Add(this.groupBox2);
            this.panel2.Controls.Add(this.groupBox1);
            this.panel2.Controls.Add(this.toolStrip1);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel2.Location = new System.Drawing.Point(0, 163);
            this.panel2.Margin = new System.Windows.Forms.Padding(4);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(584, 592);
            this.panel2.TabIndex = 2;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.gbScan);
            this.groupBox3.Controls.Add(this.gbAlign);
            this.groupBox3.Controls.Add(this.gbAdjust);
            this.groupBox3.Controls.Add(this.label18);
            this.groupBox3.Controls.Add(this.label17);
            this.groupBox3.Controls.Add(this.nudScale);
            this.groupBox3.Controls.Add(this.nudDistance);
            this.groupBox3.Controls.Add(this.nudDeviation);
            this.groupBox3.Controls.Add(this.label27);
            this.groupBox3.Location = new System.Drawing.Point(7, 191);
            this.groupBox3.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox3.Size = new System.Drawing.Size(563, 160);
            this.groupBox3.TabIndex = 18;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Frame processing";
            // 
            // gbScan
            // 
            this.gbScan.Controls.Add(this.btnScanClip);
            this.gbScan.Controls.Add(this.btnScanScene);
            this.gbScan.Controls.Add(this.btnScanFrame);
            this.gbScan.Location = new System.Drawing.Point(191, 23);
            this.gbScan.Margin = new System.Windows.Forms.Padding(4);
            this.gbScan.Name = "gbScan";
            this.gbScan.Padding = new System.Windows.Forms.Padding(4);
            this.gbScan.Size = new System.Drawing.Size(84, 130);
            this.gbScan.TabIndex = 3;
            this.gbScan.TabStop = false;
            this.gbScan.Text = "Scan";
            // 
            // btnScanClip
            // 
            this.btnScanClip.BackColor = System.Drawing.SystemColors.MenuBar;
            this.btnScanClip.Location = new System.Drawing.Point(8, 96);
            this.btnScanClip.Margin = new System.Windows.Forms.Padding(4);
            this.btnScanClip.Name = "btnScanClip";
            this.btnScanClip.Size = new System.Drawing.Size(68, 28);
            this.btnScanClip.TabIndex = 0;
            this.btnScanClip.Text = "Clip";
            this.btnScanClip.UseVisualStyleBackColor = false;
            this.btnScanClip.Click += new System.EventHandler(this.btnPanScanFull_Click);
            // 
            // btnScanScene
            // 
            this.btnScanScene.BackColor = System.Drawing.SystemColors.Info;
            this.btnScanScene.Location = new System.Drawing.Point(8, 60);
            this.btnScanScene.Margin = new System.Windows.Forms.Padding(4);
            this.btnScanScene.Name = "btnScanScene";
            this.btnScanScene.Size = new System.Drawing.Size(68, 28);
            this.btnScanScene.TabIndex = 0;
            this.btnScanScene.Text = "Scene";
            this.btnScanScene.UseVisualStyleBackColor = false;
            this.btnScanScene.Click += new System.EventHandler(this.brnPanScan_Click);
            // 
            // btnScanFrame
            // 
            this.btnScanFrame.BackColor = System.Drawing.SystemColors.InactiveBorder;
            this.btnScanFrame.Location = new System.Drawing.Point(8, 25);
            this.btnScanFrame.Margin = new System.Windows.Forms.Padding(4);
            this.btnScanFrame.Name = "btnScanFrame";
            this.btnScanFrame.Size = new System.Drawing.Size(68, 28);
            this.btnScanFrame.TabIndex = 0;
            this.btnScanFrame.Text = "Frame";
            this.btnScanFrame.UseVisualStyleBackColor = false;
            this.btnScanFrame.Click += new System.EventHandler(this.btnScanFrame_Click);
            // 
            // gbAlign
            // 
            this.gbAlign.Controls.Add(this.btnAlignScene);
            this.gbAlign.Controls.Add(this.btnAlignSingle);
            this.gbAlign.Controls.Add(this.btnAlignFrame);
            this.gbAlign.Location = new System.Drawing.Point(7, 23);
            this.gbAlign.Margin = new System.Windows.Forms.Padding(4);
            this.gbAlign.Name = "gbAlign";
            this.gbAlign.Padding = new System.Windows.Forms.Padding(4);
            this.gbAlign.Size = new System.Drawing.Size(84, 130);
            this.gbAlign.TabIndex = 3;
            this.gbAlign.TabStop = false;
            this.gbAlign.Text = "Align";
            // 
            // btnAlignScene
            // 
            this.btnAlignScene.BackColor = System.Drawing.SystemColors.MenuBar;
            this.btnAlignScene.Location = new System.Drawing.Point(8, 96);
            this.btnAlignScene.Margin = new System.Windows.Forms.Padding(4);
            this.btnAlignScene.Name = "btnAlignScene";
            this.btnAlignScene.Size = new System.Drawing.Size(68, 28);
            this.btnAlignScene.TabIndex = 0;
            this.btnAlignScene.Text = "Scene";
            this.btnAlignScene.UseVisualStyleBackColor = false;
            this.btnAlignScene.Click += new System.EventHandler(this.btnAutoOverlayScene_Click);
            // 
            // btnAlignSingle
            // 
            this.btnAlignSingle.BackColor = System.Drawing.SystemColors.Info;
            this.btnAlignSingle.Location = new System.Drawing.Point(8, 60);
            this.btnAlignSingle.Margin = new System.Windows.Forms.Padding(4);
            this.btnAlignSingle.Name = "btnAlignSingle";
            this.btnAlignSingle.Size = new System.Drawing.Size(68, 28);
            this.btnAlignSingle.TabIndex = 0;
            this.btnAlignSingle.Text = "Single";
            this.btnAlignSingle.UseVisualStyleBackColor = false;
            this.btnAlignSingle.Click += new System.EventHandler(this.btnAutoOverlaySeparatedFrame_Click);
            // 
            // btnAlignFrame
            // 
            this.btnAlignFrame.BackColor = System.Drawing.SystemColors.InactiveBorder;
            this.btnAlignFrame.Location = new System.Drawing.Point(8, 25);
            this.btnAlignFrame.Margin = new System.Windows.Forms.Padding(4);
            this.btnAlignFrame.Name = "btnAlignFrame";
            this.btnAlignFrame.Size = new System.Drawing.Size(68, 28);
            this.btnAlignFrame.TabIndex = 0;
            this.btnAlignFrame.Text = "Frame";
            this.btnAlignFrame.UseVisualStyleBackColor = false;
            this.btnAlignFrame.Click += new System.EventHandler(this.btnAutoOverlaySingleFrame_Click);
            // 
            // gbAdjust
            // 
            this.gbAdjust.Controls.Add(this.btnAdjustClip);
            this.gbAdjust.Controls.Add(this.btnAdjustScene);
            this.gbAdjust.Controls.Add(this.btnAdjustFrame);
            this.gbAdjust.Location = new System.Drawing.Point(99, 23);
            this.gbAdjust.Margin = new System.Windows.Forms.Padding(4);
            this.gbAdjust.Name = "gbAdjust";
            this.gbAdjust.Padding = new System.Windows.Forms.Padding(4);
            this.gbAdjust.Size = new System.Drawing.Size(84, 130);
            this.gbAdjust.TabIndex = 3;
            this.gbAdjust.TabStop = false;
            this.gbAdjust.Text = "Adjust";
            // 
            // btnAdjustClip
            // 
            this.btnAdjustClip.BackColor = System.Drawing.SystemColors.MenuBar;
            this.btnAdjustClip.Location = new System.Drawing.Point(8, 96);
            this.btnAdjustClip.Margin = new System.Windows.Forms.Padding(4);
            this.btnAdjustClip.Name = "btnAdjustClip";
            this.btnAdjustClip.Size = new System.Drawing.Size(68, 28);
            this.btnAdjustClip.TabIndex = 0;
            this.btnAdjustClip.Text = "Clip";
            this.btnAdjustClip.UseVisualStyleBackColor = false;
            this.btnAdjustClip.Click += new System.EventHandler(this.btnAdjustClip_Click);
            // 
            // btnAdjustScene
            // 
            this.btnAdjustScene.BackColor = System.Drawing.SystemColors.Info;
            this.btnAdjustScene.Location = new System.Drawing.Point(8, 60);
            this.btnAdjustScene.Margin = new System.Windows.Forms.Padding(4);
            this.btnAdjustScene.Name = "btnAdjustScene";
            this.btnAdjustScene.Size = new System.Drawing.Size(68, 28);
            this.btnAdjustScene.TabIndex = 0;
            this.btnAdjustScene.Text = "Scene";
            this.btnAdjustScene.UseVisualStyleBackColor = false;
            this.btnAdjustScene.Click += new System.EventHandler(this.btnAdjust_Click);
            // 
            // btnAdjustFrame
            // 
            this.btnAdjustFrame.BackColor = System.Drawing.SystemColors.InactiveBorder;
            this.btnAdjustFrame.Location = new System.Drawing.Point(8, 25);
            this.btnAdjustFrame.Margin = new System.Windows.Forms.Padding(4);
            this.btnAdjustFrame.Name = "btnAdjustFrame";
            this.btnAdjustFrame.Size = new System.Drawing.Size(68, 28);
            this.btnAdjustFrame.TabIndex = 0;
            this.btnAdjustFrame.Text = "Frame";
            this.btnAdjustFrame.UseVisualStyleBackColor = false;
            this.btnAdjustFrame.Click += new System.EventHandler(this.btnAdjustFrame_Click);
            // 
            // label18
            // 
            this.label18.AutoSize = true;
            this.label18.Location = new System.Drawing.Point(327, 63);
            this.label18.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(62, 16);
            this.label18.TabIndex = 2;
            this.label18.Text = "Scale, ‰";
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.Location = new System.Drawing.Point(307, 31);
            this.label17.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(80, 16);
            this.label17.TabIndex = 2;
            this.label17.Text = "Distance, px";
            // 
            // nudScale
            // 
            this.nudScale.Location = new System.Drawing.Point(403, 60);
            this.nudScale.Margin = new System.Windows.Forms.Padding(4);
            this.nudScale.Maximum = new decimal(new int[] {
            999,
            0,
            0,
            0});
            this.nudScale.Name = "nudScale";
            this.nudScale.Size = new System.Drawing.Size(60, 22);
            this.nudScale.TabIndex = 1;
            this.nudScale.Value = new decimal(new int[] {
            3,
            0,
            0,
            0});
            // 
            // nudDistance
            // 
            this.nudDistance.Location = new System.Drawing.Point(403, 28);
            this.nudDistance.Margin = new System.Windows.Forms.Padding(4);
            this.nudDistance.Name = "nudDistance";
            this.nudDistance.Size = new System.Drawing.Size(60, 22);
            this.nudDistance.TabIndex = 1;
            this.nudDistance.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            // 
            // nudDeviation
            // 
            this.nudDeviation.DecimalPlaces = 2;
            this.nudDeviation.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.nudDeviation.Location = new System.Drawing.Point(403, 92);
            this.nudDeviation.Margin = new System.Windows.Forms.Padding(4);
            this.nudDeviation.Maximum = new decimal(new int[] {
            99,
            0,
            0,
            0});
            this.nudDeviation.Name = "nudDeviation";
            this.nudDeviation.Size = new System.Drawing.Size(60, 22);
            this.nudDeviation.TabIndex = 11;
            this.nudDeviation.ValueChanged += new System.EventHandler(this.Reset);
            // 
            // label27
            // 
            this.label27.AutoSize = true;
            this.label27.Location = new System.Drawing.Point(297, 95);
            this.label27.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label27.Name = "label27";
            this.label27.Size = new System.Drawing.Size(90, 16);
            this.label27.TabIndex = 3;
            this.label27.Text = "Max deviation";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label25);
            this.groupBox2.Controls.Add(this.chbColorAdjust);
            this.groupBox2.Controls.Add(this.chbRGB);
            this.groupBox2.Controls.Add(this.tbColorAdjust);
            this.groupBox2.Controls.Add(this.tbOpacity);
            this.groupBox2.Controls.Add(this.chbDebug);
            this.groupBox2.Controls.Add(this.chbPreview);
            this.groupBox2.Controls.Add(this.cbMatrix);
            this.groupBox2.Controls.Add(this.cbOverlayMode);
            this.groupBox2.Controls.Add(this.cbMode);
            this.groupBox2.Controls.Add(this.label23);
            this.groupBox2.Controls.Add(this.label24);
            this.groupBox2.Controls.Add(this.label6);
            this.groupBox2.Controls.Add(this.nudNoiseSize);
            this.groupBox2.Controls.Add(this.nudGradientSize);
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.label11);
            this.groupBox2.Controls.Add(this.nudOutputWidth);
            this.groupBox2.Controls.Add(this.label5);
            this.groupBox2.Controls.Add(this.nudOutputHeight);
            this.groupBox2.Controls.Add(this.label26);
            this.groupBox2.Controls.Add(this.label22);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Location = new System.Drawing.Point(7, 361);
            this.groupBox2.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox2.Size = new System.Drawing.Size(563, 193);
            this.groupBox2.TabIndex = 17;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Display settings";
            // 
            // label25
            // 
            this.label25.AutoSize = true;
            this.label25.Location = new System.Drawing.Point(500, 160);
            this.label25.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label25.Name = "label25";
            this.label25.Size = new System.Drawing.Size(52, 16);
            this.label25.TabIndex = 3;
            this.label25.Text = "overlay";
            // 
            // chbColorAdjust
            // 
            this.chbColorAdjust.AutoSize = true;
            this.chbColorAdjust.Location = new System.Drawing.Point(251, 160);
            this.chbColorAdjust.Margin = new System.Windows.Forms.Padding(4);
            this.chbColorAdjust.Name = "chbColorAdjust";
            this.chbColorAdjust.Size = new System.Drawing.Size(18, 17);
            this.chbColorAdjust.TabIndex = 9;
            this.chbColorAdjust.UseVisualStyleBackColor = true;
            this.chbColorAdjust.CheckedChanged += new System.EventHandler(this.Render);
            // 
            // chbRGB
            // 
            this.chbRGB.AutoSize = true;
            this.chbRGB.Location = new System.Drawing.Point(461, 57);
            this.chbRGB.Margin = new System.Windows.Forms.Padding(4);
            this.chbRGB.Name = "chbRGB";
            this.chbRGB.Size = new System.Drawing.Size(58, 20);
            this.chbRGB.TabIndex = 9;
            this.chbRGB.Text = "RGB";
            this.chbRGB.UseVisualStyleBackColor = true;
            this.chbRGB.CheckedChanged += new System.EventHandler(this.Render);
            // 
            // tbColorAdjust
            // 
            this.tbColorAdjust.LargeChange = 25;
            this.tbColorAdjust.Location = new System.Drawing.Point(343, 153);
            this.tbColorAdjust.Margin = new System.Windows.Forms.Padding(4);
            this.tbColorAdjust.Maximum = 100;
            this.tbColorAdjust.Name = "tbColorAdjust";
            this.tbColorAdjust.Size = new System.Drawing.Size(151, 56);
            this.tbColorAdjust.SmallChange = 10;
            this.tbColorAdjust.TabIndex = 8;
            this.tbColorAdjust.TickFrequency = 50;
            this.tbColorAdjust.Value = 100;
            this.tbColorAdjust.ValueChanged += new System.EventHandler(this.Render);
            // 
            // tbOpacity
            // 
            this.tbOpacity.LargeChange = 25;
            this.tbOpacity.Location = new System.Drawing.Point(336, 90);
            this.tbOpacity.Margin = new System.Windows.Forms.Padding(4);
            this.tbOpacity.Maximum = 100;
            this.tbOpacity.Name = "tbOpacity";
            this.tbOpacity.Size = new System.Drawing.Size(157, 56);
            this.tbOpacity.SmallChange = 10;
            this.tbOpacity.TabIndex = 8;
            this.tbOpacity.TickFrequency = 50;
            this.tbOpacity.Value = 100;
            this.tbOpacity.ValueChanged += new System.EventHandler(this.Render);
            // 
            // chbDebug
            // 
            this.chbDebug.AutoSize = true;
            this.chbDebug.Checked = true;
            this.chbDebug.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chbDebug.Location = new System.Drawing.Point(379, 23);
            this.chbDebug.Margin = new System.Windows.Forms.Padding(4);
            this.chbDebug.Name = "chbDebug";
            this.chbDebug.Size = new System.Drawing.Size(99, 20);
            this.chbDebug.TabIndex = 7;
            this.chbDebug.Text = "Display info";
            this.chbDebug.UseVisualStyleBackColor = true;
            this.chbDebug.CheckedChanged += new System.EventHandler(this.Render);
            // 
            // chbPreview
            // 
            this.chbPreview.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chbPreview.AutoSize = true;
            this.chbPreview.Checked = true;
            this.chbPreview.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chbPreview.Location = new System.Drawing.Point(301, 23);
            this.chbPreview.Margin = new System.Windows.Forms.Padding(4);
            this.chbPreview.Name = "chbPreview";
            this.chbPreview.Size = new System.Drawing.Size(77, 20);
            this.chbPreview.TabIndex = 3;
            this.chbPreview.Text = "Preview";
            this.chbPreview.UseVisualStyleBackColor = true;
            this.chbPreview.CheckedChanged += new System.EventHandler(this.Render);
            // 
            // cbMatrix
            // 
            this.cbMatrix.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbMatrix.FormattingEnabled = true;
            this.cbMatrix.Location = new System.Drawing.Point(343, 54);
            this.cbMatrix.Margin = new System.Windows.Forms.Padding(4);
            this.cbMatrix.Name = "cbMatrix";
            this.cbMatrix.Size = new System.Drawing.Size(109, 24);
            this.cbMatrix.TabIndex = 6;
            this.cbMatrix.SelectedIndexChanged += new System.EventHandler(this.Render);
            // 
            // cbOverlayMode
            // 
            this.cbOverlayMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbOverlayMode.FormattingEnabled = true;
            this.cbOverlayMode.Items.AddRange(new object[] {
            "Blend",
            "Difference",
            "Subtract",
            "Luma",
            "Chroma",
            "Add",
            "Multiply",
            "Lighten",
            "Darken",
            "SoftLight",
            "HardLight",
            "Exclusion"});
            this.cbOverlayMode.Location = new System.Drawing.Point(77, 87);
            this.cbOverlayMode.Margin = new System.Windows.Forms.Padding(4);
            this.cbOverlayMode.Name = "cbOverlayMode";
            this.cbOverlayMode.Size = new System.Drawing.Size(191, 24);
            this.cbOverlayMode.TabIndex = 6;
            this.cbOverlayMode.SelectedIndexChanged += new System.EventHandler(this.Render);
            // 
            // cbMode
            // 
            this.cbMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbMode.FormattingEnabled = true;
            this.cbMode.Location = new System.Drawing.Point(77, 54);
            this.cbMode.Margin = new System.Windows.Forms.Padding(4);
            this.cbMode.Name = "cbMode";
            this.cbMode.Size = new System.Drawing.Size(191, 24);
            this.cbMode.TabIndex = 6;
            this.cbMode.SelectedIndexChanged += new System.EventHandler(this.Render);
            // 
            // label23
            // 
            this.label23.AutoSize = true;
            this.label23.Location = new System.Drawing.Point(159, 160);
            this.label23.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label23.Name = "label23";
            this.label23.Size = new System.Drawing.Size(79, 16);
            this.label23.TabIndex = 3;
            this.label23.Text = "Color Adjust";
            // 
            // label24
            // 
            this.label24.AutoSize = true;
            this.label24.Location = new System.Drawing.Point(283, 160);
            this.label24.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label24.Name = "label24";
            this.label24.Size = new System.Drawing.Size(48, 16);
            this.label24.TabIndex = 3;
            this.label24.Text = "source";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(277, 97);
            this.label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(53, 16);
            this.label6.TabIndex = 3;
            this.label6.Text = "Opacity";
            // 
            // nudNoiseSize
            // 
            this.nudNoiseSize.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.nudNoiseSize.Location = new System.Drawing.Point(201, 121);
            this.nudNoiseSize.Margin = new System.Windows.Forms.Padding(4);
            this.nudNoiseSize.Maximum = new decimal(new int[] {
            500,
            0,
            0,
            0});
            this.nudNoiseSize.Name = "nudNoiseSize";
            this.nudNoiseSize.Size = new System.Drawing.Size(68, 22);
            this.nudNoiseSize.TabIndex = 5;
            this.nudNoiseSize.ValueChanged += new System.EventHandler(this.Render);
            // 
            // nudGradientSize
            // 
            this.nudGradientSize.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.nudGradientSize.Location = new System.Drawing.Point(77, 121);
            this.nudGradientSize.Margin = new System.Windows.Forms.Padding(4);
            this.nudGradientSize.Maximum = new decimal(new int[] {
            500,
            0,
            0,
            0});
            this.nudGradientSize.Name = "nudGradientSize";
            this.nudGradientSize.Size = new System.Drawing.Size(68, 22);
            this.nudGradientSize.TabIndex = 5;
            this.nudGradientSize.ValueChanged += new System.EventHandler(this.Render);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(8, 25);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(33, 16);
            this.label1.TabIndex = 3;
            this.label1.Text = "Size";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(147, 25);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(13, 16);
            this.label2.TabIndex = 2;
            this.label2.Text = "x";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(153, 123);
            this.label11.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(43, 16);
            this.label11.TabIndex = 3;
            this.label11.Text = "Noise";
            // 
            // nudOutputWidth
            // 
            this.nudOutputWidth.Location = new System.Drawing.Point(77, 22);
            this.nudOutputWidth.Margin = new System.Windows.Forms.Padding(4);
            this.nudOutputWidth.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.nudOutputWidth.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.nudOutputWidth.Name = "nudOutputWidth";
            this.nudOutputWidth.Size = new System.Drawing.Size(68, 22);
            this.nudOutputWidth.TabIndex = 5;
            this.nudOutputWidth.Value = new decimal(new int[] {
            1920,
            0,
            0,
            0});
            this.nudOutputWidth.ValueChanged += new System.EventHandler(this.Render);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(8, 123);
            this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(58, 16);
            this.label5.TabIndex = 3;
            this.label5.Text = "Gradient";
            // 
            // nudOutputHeight
            // 
            this.nudOutputHeight.Location = new System.Drawing.Point(163, 22);
            this.nudOutputHeight.Margin = new System.Windows.Forms.Padding(4);
            this.nudOutputHeight.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.nudOutputHeight.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.nudOutputHeight.Name = "nudOutputHeight";
            this.nudOutputHeight.Size = new System.Drawing.Size(68, 22);
            this.nudOutputHeight.TabIndex = 4;
            this.nudOutputHeight.Value = new decimal(new int[] {
            1080,
            0,
            0,
            0});
            this.nudOutputHeight.ValueChanged += new System.EventHandler(this.Render);
            // 
            // label26
            // 
            this.label26.AutoSize = true;
            this.label26.Location = new System.Drawing.Point(8, 91);
            this.label26.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label26.Name = "label26";
            this.label26.Size = new System.Drawing.Size(54, 16);
            this.label26.TabIndex = 3;
            this.label26.Text = "Overlay";
            // 
            // label22
            // 
            this.label22.AutoSize = true;
            this.label22.Location = new System.Drawing.Point(288, 58);
            this.label22.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label22.Name = "label22";
            this.label22.Size = new System.Drawing.Size(42, 16);
            this.label22.TabIndex = 3;
            this.label22.Text = "Matrix";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(8, 58);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(56, 16);
            this.label3.TabIndex = 3;
            this.label3.Text = "Framing";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.btnFix);
            this.groupBox1.Controls.Add(this.tbWarp);
            this.groupBox1.Controls.Add(this.label29);
            this.groupBox1.Controls.Add(this.nudCompare);
            this.groupBox1.Controls.Add(this.btnCompare);
            this.groupBox1.Controls.Add(this.nudX);
            this.groupBox1.Controls.Add(this.label8);
            this.groupBox1.Controls.Add(this.nudY);
            this.groupBox1.Controls.Add(this.chbOverlaySizeSync);
            this.groupBox1.Controls.Add(this.nudAngle);
            this.groupBox1.Controls.Add(this.nudOverlayHeight);
            this.groupBox1.Controls.Add(this.label9);
            this.groupBox1.Controls.Add(this.nudOverlayWidth);
            this.groupBox1.Controls.Add(this.label14);
            this.groupBox1.Controls.Add(this.label28);
            this.groupBox1.Controls.Add(this.label13);
            this.groupBox1.Controls.Add(this.label12);
            this.groupBox1.Location = new System.Drawing.Point(7, 7);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(4);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(4);
            this.groupBox1.Size = new System.Drawing.Size(563, 176);
            this.groupBox1.TabIndex = 16;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Overlay settings";
            // 
            // btnFix
            // 
            this.btnFix.Location = new System.Drawing.Point(369, 21);
            this.btnFix.Margin = new System.Windows.Forms.Padding(4);
            this.btnFix.Name = "btnFix";
            this.btnFix.Size = new System.Drawing.Size(81, 28);
            this.btnFix.TabIndex = 18;
            this.btnFix.Text = "Fix";
            this.btnFix.UseVisualStyleBackColor = true;
            this.btnFix.Click += new System.EventHandler(this.btnFix_Click);
            // 
            // tbWarp
            // 
            this.tbWarp.Location = new System.Drawing.Point(67, 85);
            this.tbWarp.Margin = new System.Windows.Forms.Padding(4);
            this.tbWarp.Multiline = true;
            this.tbWarp.Name = "tbWarp";
            this.tbWarp.Size = new System.Drawing.Size(487, 80);
            this.tbWarp.TabIndex = 17;
            this.tbWarp.TextChanged += new System.EventHandler(this.Render);
            // 
            // label29
            // 
            this.label29.AutoSize = true;
            this.label29.Location = new System.Drawing.Point(12, 113);
            this.label29.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label29.Name = "label29";
            this.label29.Size = new System.Drawing.Size(40, 16);
            this.label29.TabIndex = 16;
            this.label29.Text = "Warp";
            // 
            // nudCompare
            // 
            this.nudCompare.DecimalPlaces = 4;
            this.nudCompare.Increment = new decimal(new int[] {
            1,
            0,
            0,
            196608});
            this.nudCompare.Location = new System.Drawing.Point(457, 55);
            this.nudCompare.Margin = new System.Windows.Forms.Padding(4);
            this.nudCompare.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.nudCompare.Name = "nudCompare";
            this.nudCompare.Size = new System.Drawing.Size(97, 22);
            this.nudCompare.TabIndex = 15;
            this.nudCompare.Value = new decimal(new int[] {
            999,
            0,
            0,
            196608});
            this.nudCompare.ValueChanged += new System.EventHandler(this.Compare);
            // 
            // btnCompare
            // 
            this.btnCompare.Location = new System.Drawing.Point(457, 21);
            this.btnCompare.Margin = new System.Windows.Forms.Padding(4);
            this.btnCompare.Name = "btnCompare";
            this.btnCompare.Size = new System.Drawing.Size(97, 28);
            this.btnCompare.TabIndex = 14;
            this.btnCompare.Text = "Compare";
            this.btnCompare.UseVisualStyleBackColor = true;
            this.btnCompare.Click += new System.EventHandler(this.btnCompare_Click);
            // 
            // nudX
            // 
            this.nudX.DecimalPlaces = 2;
            this.nudX.Increment = new decimal(new int[] {
            25,
            0,
            0,
            131072});
            this.nudX.Location = new System.Drawing.Point(32, 23);
            this.nudX.Margin = new System.Windows.Forms.Padding(4);
            this.nudX.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.nudX.Minimum = new decimal(new int[] {
            10000,
            0,
            0,
            -2147483648});
            this.nudX.Name = "nudX";
            this.nudX.Size = new System.Drawing.Size(80, 22);
            this.nudX.TabIndex = 6;
            this.nudX.ValueChanged += new System.EventHandler(this.Render);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(5, 26);
            this.label8.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(15, 16);
            this.label8.TabIndex = 7;
            this.label8.Text = "X";
            // 
            // nudY
            // 
            this.nudY.DecimalPlaces = 2;
            this.nudY.Increment = new decimal(new int[] {
            25,
            0,
            0,
            131072});
            this.nudY.Location = new System.Drawing.Point(142, 23);
            this.nudY.Margin = new System.Windows.Forms.Padding(4);
            this.nudY.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.nudY.Minimum = new decimal(new int[] {
            10000,
            0,
            0,
            -2147483648});
            this.nudY.Name = "nudY";
            this.nudY.Size = new System.Drawing.Size(80, 22);
            this.nudY.TabIndex = 6;
            this.nudY.ValueChanged += new System.EventHandler(this.Render);
            // 
            // chbOverlaySizeSync
            // 
            this.chbOverlaySizeSync.AutoSize = true;
            this.chbOverlaySizeSync.Checked = true;
            this.chbOverlaySizeSync.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chbOverlaySizeSync.Location = new System.Drawing.Point(278, 56);
            this.chbOverlaySizeSync.Margin = new System.Windows.Forms.Padding(4);
            this.chbOverlaySizeSync.Name = "chbOverlaySizeSync";
            this.chbOverlaySizeSync.Size = new System.Drawing.Size(59, 20);
            this.chbOverlaySizeSync.TabIndex = 12;
            this.chbOverlaySizeSync.Text = "Sync";
            this.chbOverlaySizeSync.UseVisualStyleBackColor = true;
            this.chbOverlaySizeSync.CheckedChanged += new System.EventHandler(this.Render);
            // 
            // nudAngle
            // 
            this.nudAngle.DecimalPlaces = 2;
            this.nudAngle.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.nudAngle.Location = new System.Drawing.Point(293, 23);
            this.nudAngle.Margin = new System.Windows.Forms.Padding(4);
            this.nudAngle.Maximum = new decimal(new int[] {
            360,
            0,
            0,
            0});
            this.nudAngle.Minimum = new decimal(new int[] {
            360,
            0,
            0,
            -2147483648});
            this.nudAngle.Name = "nudAngle";
            this.nudAngle.Size = new System.Drawing.Size(68, 22);
            this.nudAngle.TabIndex = 6;
            this.nudAngle.ValueChanged += new System.EventHandler(this.Render);
            // 
            // nudOverlayHeight
            // 
            this.nudOverlayHeight.DecimalPlaces = 2;
            this.nudOverlayHeight.Location = new System.Drawing.Point(187, 55);
            this.nudOverlayHeight.Margin = new System.Windows.Forms.Padding(4);
            this.nudOverlayHeight.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.nudOverlayHeight.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.nudOverlayHeight.Name = "nudOverlayHeight";
            this.nudOverlayHeight.Size = new System.Drawing.Size(80, 22);
            this.nudOverlayHeight.TabIndex = 10;
            this.nudOverlayHeight.Value = new decimal(new int[] {
            1080,
            0,
            0,
            0});
            this.nudOverlayHeight.ValueChanged += new System.EventHandler(this.Render);
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(118, 26);
            this.label9.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(16, 16);
            this.label9.TabIndex = 7;
            this.label9.Text = "Y";
            // 
            // nudOverlayWidth
            // 
            this.nudOverlayWidth.DecimalPlaces = 2;
            this.nudOverlayWidth.Location = new System.Drawing.Point(91, 55);
            this.nudOverlayWidth.Margin = new System.Windows.Forms.Padding(4);
            this.nudOverlayWidth.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.nudOverlayWidth.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.nudOverlayWidth.Name = "nudOverlayWidth";
            this.nudOverlayWidth.Size = new System.Drawing.Size(80, 22);
            this.nudOverlayWidth.TabIndex = 11;
            this.nudOverlayWidth.Value = new decimal(new int[] {
            1920,
            0,
            0,
            0});
            this.nudOverlayWidth.ValueChanged += new System.EventHandler(this.Render);
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(243, 25);
            this.label14.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(42, 16);
            this.label14.TabIndex = 7;
            this.label14.Text = "Angle";
            // 
            // label28
            // 
            this.label28.AutoSize = true;
            this.label28.Location = new System.Drawing.Point(403, 57);
            this.label28.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label28.Name = "label28";
            this.label28.Size = new System.Drawing.Size(47, 16);
            this.label28.TabIndex = 9;
            this.label28.Text = "Min eq";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(173, 59);
            this.label13.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(13, 16);
            this.label13.TabIndex = 8;
            this.label13.Text = "x";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(5, 58);
            this.label12.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(81, 16);
            this.label12.TabIndex = 9;
            this.label12.Text = "Overlay size";
            // 
            // toolStrip1
            // 
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnSave,
            this.btnReset,
            this.btnResetCurrent,
            this.btnJoinTo,
            this.btnJoinNext,
            this.btnJoinPrev,
            this.btnSeparate,
            this.btnResetInterval,
            this.btnDelete});
            this.toolStrip1.Location = new System.Drawing.Point(0, 561);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(584, 31);
            this.toolStrip1.TabIndex = 14;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // btnSave
            // 
            this.btnSave.BackColor = System.Drawing.Color.Honeydew;
            this.btnSave.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnSave.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnSave.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(45, 28);
            this.btnSave.Text = "Save";
            this.btnSave.Click += new System.EventHandler(this.SaveStat);
            // 
            // btnReset
            // 
            this.btnReset.BackColor = System.Drawing.Color.MistyRose;
            this.btnReset.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnReset.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnReset.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(52, 28);
            this.btnReset.Text = "Reset";
            this.btnReset.Click += new System.EventHandler(this.Reset);
            // 
            // btnResetCurrent
            // 
            this.btnResetCurrent.BackColor = System.Drawing.Color.Linen;
            this.btnResetCurrent.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnResetCurrent.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnResetCurrent.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnResetCurrent.Name = "btnResetCurrent";
            this.btnResetCurrent.Size = new System.Drawing.Size(61, 28);
            this.btnResetCurrent.Text = "Reload";
            this.btnResetCurrent.Click += new System.EventHandler(this.ResetCurrent);
            // 
            // btnJoinTo
            // 
            this.btnJoinTo.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnJoinTo.BackColor = System.Drawing.Color.AliceBlue;
            this.btnJoinTo.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnJoinTo.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnJoinTo.Name = "btnJoinTo";
            this.btnJoinTo.Size = new System.Drawing.Size(57, 28);
            this.btnJoinTo.Text = "Join to";
            this.btnJoinTo.Click += new System.EventHandler(this.btnJoinTo_Click);
            // 
            // btnJoinNext
            // 
            this.btnJoinNext.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnJoinNext.BackColor = System.Drawing.Color.AliceBlue;
            this.btnJoinNext.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnJoinNext.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnJoinNext.Name = "btnJoinNext";
            this.btnJoinNext.Size = new System.Drawing.Size(59, 28);
            this.btnJoinNext.Text = "Join ->";
            this.btnJoinNext.Click += new System.EventHandler(this.btnJoinNext_Click);
            // 
            // btnJoinPrev
            // 
            this.btnJoinPrev.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnJoinPrev.BackColor = System.Drawing.Color.AliceBlue;
            this.btnJoinPrev.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnJoinPrev.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnJoinPrev.Name = "btnJoinPrev";
            this.btnJoinPrev.Size = new System.Drawing.Size(59, 28);
            this.btnJoinPrev.Text = "<- Join";
            this.btnJoinPrev.Click += new System.EventHandler(this.btnJoinPrev_Click);
            // 
            // btnSeparate
            // 
            this.btnSeparate.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnSeparate.BackColor = System.Drawing.Color.LightBlue;
            this.btnSeparate.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnSeparate.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnSeparate.Name = "btnSeparate";
            this.btnSeparate.Size = new System.Drawing.Size(72, 28);
            this.btnSeparate.Text = "Separate";
            this.btnSeparate.Click += new System.EventHandler(this.btnSeparate_Click);
            // 
            // btnResetInterval
            // 
            this.btnResetInterval.BackColor = System.Drawing.Color.Cornsilk;
            this.btnResetInterval.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnResetInterval.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnResetInterval.Image = ((System.Drawing.Image)(resources.GetObject("btnResetInterval.Image")));
            this.btnResetInterval.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnResetInterval.Name = "btnResetInterval";
            this.btnResetInterval.Size = new System.Drawing.Size(84, 28);
            this.btnResetInterval.Text = "Reload All";
            this.btnResetInterval.Click += new System.EventHandler(this.ResetInterval);
            // 
            // btnDelete
            // 
            this.btnDelete.BackColor = System.Drawing.Color.MediumBlue;
            this.btnDelete.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnDelete.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnDelete.ForeColor = System.Drawing.Color.White;
            this.btnDelete.Image = ((System.Drawing.Image)(resources.GetObject("btnDelete.Image")));
            this.btnDelete.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(57, 28);
            this.btnDelete.Text = "Delete";
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // pictureBox
            // 
            this.pictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBox.Location = new System.Drawing.Point(0, 0);
            this.pictureBox.Margin = new System.Windows.Forms.Padding(4);
            this.pictureBox.Name = "pictureBox";
            this.pictureBox.Size = new System.Drawing.Size(993, 757);
            this.pictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox.TabIndex = 2;
            this.pictureBox.TabStop = false;
            // 
            // intervalDataGridViewTextBoxColumn
            // 
            this.intervalDataGridViewTextBoxColumn.DataPropertyName = "Interval";
            this.intervalDataGridViewTextBoxColumn.HeaderText = "Interval";
            this.intervalDataGridViewTextBoxColumn.MinimumWidth = 6;
            this.intervalDataGridViewTextBoxColumn.Name = "intervalDataGridViewTextBoxColumn";
            this.intervalDataGridViewTextBoxColumn.ReadOnly = true;
            this.intervalDataGridViewTextBoxColumn.Width = 79;
            // 
            // xDataGridViewTextBoxColumn
            // 
            this.xDataGridViewTextBoxColumn.DataPropertyName = "Placement";
            this.xDataGridViewTextBoxColumn.HeaderText = "Location";
            this.xDataGridViewTextBoxColumn.MinimumWidth = 6;
            this.xDataGridViewTextBoxColumn.Name = "xDataGridViewTextBoxColumn";
            this.xDataGridViewTextBoxColumn.ReadOnly = true;
            this.xDataGridViewTextBoxColumn.Width = 87;
            // 
            // Angle
            // 
            this.Angle.DataPropertyName = "Angle";
            this.Angle.HeaderText = "Angle";
            this.Angle.MinimumWidth = 6;
            this.Angle.Name = "Angle";
            this.Angle.ReadOnly = true;
            this.Angle.Width = 71;
            // 
            // diffDataGridViewTextBoxColumn
            // 
            this.diffDataGridViewTextBoxColumn.DataPropertyName = "Diff";
            dataGridViewCellStyle1.Format = "F1";
            this.diffDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle1;
            this.diffDataGridViewTextBoxColumn.HeaderText = "Diff";
            this.diffDataGridViewTextBoxColumn.MinimumWidth = 6;
            this.diffDataGridViewTextBoxColumn.Name = "diffDataGridViewTextBoxColumn";
            this.diffDataGridViewTextBoxColumn.ReadOnly = true;
            this.diffDataGridViewTextBoxColumn.Width = 55;
            // 
            // frameIntervalBindingSource
            // 
            this.frameIntervalBindingSource.DataSource = typeof(AutoOverlay.FrameInterval);
            // 
            // OverlayEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1579, 789);
            this.Controls.Add(this.pictureBox);
            this.Controls.Add(this.panelManage);
            this.Controls.Add(this.panel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MinimumSize = new System.Drawing.Size(1327, 826);
            this.Name = "OverlayEditor";
            this.Text = "Overlay Editor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.OverlayEditor_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.OverlayEditorForm_FormClosed);
            this.Load += new System.EventHandler(this.OverlayEditorForm_Load);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trackBar)).EndInit();
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudMaxDiff)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudMinSceneLength)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudMinFrame)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudMaxFrame)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudCurrentFrame)).EndInit();
            this.panelManage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.grid)).EndInit();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.gbScan.ResumeLayout(false);
            this.gbAlign.ResumeLayout(false);
            this.gbAdjust.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.nudScale)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDistance)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDeviation)).EndInit();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbColorAdjust)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbOpacity)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudNoiseSize)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudGradientSize)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudOutputWidth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudOutputHeight)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudCompare)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudY)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudAngle)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudOverlayHeight)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudOverlayWidth)).EndInit();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.frameIntervalBindingSource)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.TrackBar trackBar;
        private System.Windows.Forms.Panel panelManage;
        private System.Windows.Forms.DataGridView grid;
        private System.Windows.Forms.PictureBox pictureBox;
        private System.Windows.Forms.NumericUpDown nudOutputHeight;
        private System.Windows.Forms.NumericUpDown nudOutputWidth;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox cbMode;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown nudGradientSize;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.CheckBox chbDebug;
        private System.Windows.Forms.BindingSource frameIntervalBindingSource;
        private System.Windows.Forms.CheckBox chbOverlaySizeSync;
        private System.Windows.Forms.NumericUpDown nudOverlayHeight;
        private System.Windows.Forms.NumericUpDown nudOverlayWidth;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.NumericUpDown nudY;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.NumericUpDown nudX;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.NumericUpDown nudAngle;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton btnSave;
        private System.Windows.Forms.ToolStripButton btnReset;
        private System.Windows.Forms.CheckBox chbPreview;
        private System.Windows.Forms.CheckBox chbEditor;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.NumericUpDown nudCurrentFrame;
        private System.Windows.Forms.ToolStripButton btnResetCurrent;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.NumericUpDown nudNoiseSize;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.ToolStripButton btnJoinTo;
        private System.Windows.Forms.ToolStripButton btnJoinNext;
        private System.Windows.Forms.ToolStripButton btnJoinPrev;
        private System.Windows.Forms.ToolStripButton btnSeparate;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.NumericUpDown nudScale;
        private System.Windows.Forms.NumericUpDown nudDistance;
        private System.Windows.Forms.ToolStripButton btnDelete;
        private System.Windows.Forms.NumericUpDown nudMinFrame;
        private System.Windows.Forms.NumericUpDown nudMaxFrame;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.ComboBox cbMatrix;
        private System.Windows.Forms.Label label22;
        private System.Windows.Forms.TrackBar tbOpacity;
        private System.Windows.Forms.CheckBox chbRGB;
        private System.Windows.Forms.Label label25;
        private System.Windows.Forms.CheckBox chbColorAdjust;
        private System.Windows.Forms.TrackBar tbColorAdjust;
        private System.Windows.Forms.Label label23;
        private System.Windows.Forms.Label label24;
        private System.Windows.Forms.CheckBox chbDefective;
        private System.Windows.Forms.Button btnCompare;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.NumericUpDown nudCompare;
        private System.Windows.Forms.ComboBox cbOverlayMode;
        private System.Windows.Forms.Label label26;
        private System.Windows.Forms.NumericUpDown nudDeviation;
        private System.Windows.Forms.Label label27;
        private System.Windows.Forms.Label label28;
        private System.Windows.Forms.TextBox tbWarp;
        private System.Windows.Forms.Label label29;
        private System.Windows.Forms.Button btnFix;
        private System.Windows.Forms.Button btnAdjustFrame;
        private System.Windows.Forms.GroupBox gbAdjust;
        private System.Windows.Forms.Button btnAdjustClip;
        private System.Windows.Forms.Button btnAdjustScene;
        private System.Windows.Forms.GroupBox gbScan;
        private System.Windows.Forms.Button btnScanClip;
        private System.Windows.Forms.Button btnScanScene;
        private System.Windows.Forms.Button btnScanFrame;
        private System.Windows.Forms.GroupBox gbAlign;
        private System.Windows.Forms.Button btnAlignScene;
        private System.Windows.Forms.Button btnAlignSingle;
        private System.Windows.Forms.Button btnAlignFrame;
        private System.Windows.Forms.NumericUpDown nudMinSceneLength;
        private System.Windows.Forms.Label label30;
        private System.Windows.Forms.NumericUpDown nudMaxDiff;
        private System.Windows.Forms.Label label31;
        private System.Windows.Forms.DataGridViewTextBoxColumn intervalDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn xDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn Resolution;
        private System.Windows.Forms.DataGridViewTextBoxColumn Angle;
        private System.Windows.Forms.DataGridViewTextBoxColumn diffDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewCheckBoxColumn Fixed;
        private System.Windows.Forms.ToolStripButton btnResetInterval;
    }
}