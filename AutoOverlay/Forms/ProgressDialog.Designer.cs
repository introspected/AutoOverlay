namespace AutoOverlay.Forms
{
    partial class ProgressDialog
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ProgressDialog));
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnCancel = new System.Windows.Forms.ToolStripButton();
            this.btnPause = new System.Windows.Forms.ToolStripButton();
            this.btnResume = new System.Windows.Forms.ToolStripButton();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.labelFps = new System.Windows.Forms.Label();
            this.labelFrames = new System.Windows.Forms.Label();
            this.labelEta = new System.Windows.Forms.Label();
            this.labelElapsed = new System.Windows.Forms.Label();
            this.timer = new System.Windows.Forms.Timer(this.components);
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnCancel,
            this.btnPause,
            this.btnResume});
            this.toolStrip1.Location = new System.Drawing.Point(0, 110);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(512, 27);
            this.toolStrip1.TabIndex = 0;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // btnCancel
            // 
            this.btnCancel.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnCancel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnCancel.Image = ((System.Drawing.Image)(resources.GetObject("btnCancel.Image")));
            this.btnCancel.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(57, 24);
            this.btnCancel.Text = "Cancel";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnPause
            // 
            this.btnPause.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnPause.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnPause.Image = ((System.Drawing.Image)(resources.GetObject("btnPause.Image")));
            this.btnPause.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnPause.Name = "btnPause";
            this.btnPause.Size = new System.Drawing.Size(50, 24);
            this.btnPause.Text = "Pause";
            this.btnPause.Click += new System.EventHandler(this.btnPause_Click);
            // 
            // btnResume
            // 
            this.btnResume.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnResume.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnResume.Image = ((System.Drawing.Image)(resources.GetObject("btnResume.Image")));
            this.btnResume.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnResume.Name = "btnResume";
            this.btnResume.Size = new System.Drawing.Size(65, 24);
            this.btnResume.Text = "Resume";
            this.btnResume.Visible = false;
            this.btnResume.Click += new System.EventHandler(this.btnResume_Click);
            // 
            // progressBar
            // 
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.Location = new System.Drawing.Point(16, 41);
            this.progressBar.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(480, 28);
            this.progressBar.TabIndex = 1;
            // 
            // labelFps
            // 
            this.labelFps.Location = new System.Drawing.Point(12, 11);
            this.labelFps.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelFps.Name = "labelFps";
            this.labelFps.Size = new System.Drawing.Size(163, 16);
            this.labelFps.TabIndex = 2;
            this.labelFps.Text = "FPS";
            // 
            // labelFrames
            // 
            this.labelFrames.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelFrames.Location = new System.Drawing.Point(296, 11);
            this.labelFrames.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelFrames.Name = "labelFrames";
            this.labelFrames.Size = new System.Drawing.Size(200, 21);
            this.labelFrames.TabIndex = 2;
            this.labelFrames.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // labelEta
            // 
            this.labelEta.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelEta.Location = new System.Drawing.Point(340, 73);
            this.labelEta.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelEta.Name = "labelEta";
            this.labelEta.Size = new System.Drawing.Size(156, 33);
            this.labelEta.TabIndex = 2;
            this.labelEta.Text = "Remaining";
            this.labelEta.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // labelElapsed
            // 
            this.labelElapsed.AutoSize = true;
            this.labelElapsed.Location = new System.Drawing.Point(12, 81);
            this.labelElapsed.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelElapsed.Name = "labelElapsed";
            this.labelElapsed.Size = new System.Drawing.Size(58, 16);
            this.labelElapsed.TabIndex = 2;
            this.labelElapsed.Text = "Elapsed";
            this.labelElapsed.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // timer
            // 
            this.timer.Interval = 250;
            this.timer.Tick += new System.EventHandler(this.UpdateControls);
            // 
            // ProgressDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(512, 137);
            this.Controls.Add(this.labelElapsed);
            this.Controls.Add(this.labelEta);
            this.Controls.Add(this.labelFrames);
            this.Controls.Add(this.labelFps);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.toolStrip1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ProgressDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Status";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.ProgressDialog_FormClosed);
            this.Load += new System.EventHandler(this.ProgressDialog_Load);
            this.Shown += new System.EventHandler(this.ProgressDialog_Shown);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton btnCancel;
        private System.Windows.Forms.ToolStripButton btnPause;
        private System.Windows.Forms.ToolStripButton btnResume;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label labelFps;
        private System.Windows.Forms.Label labelFrames;
        private System.Windows.Forms.Label labelEta;
        private System.Windows.Forms.Label labelElapsed;
        private System.Windows.Forms.Timer timer;
    }
}