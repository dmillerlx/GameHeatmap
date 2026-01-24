namespace GameHeatmap
{
    partial class ChessBoardViewer
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ChessBoardViewer));
            this.timerCheckDone = new System.Windows.Forms.Timer(this.components);
            this.timerStatus = new System.Windows.Forms.Timer(this.components);
            this.timerLearnMode = new System.Windows.Forms.Timer(this.components);
            this.pictureBoxTop = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxTop)).BeginInit();
            this.SuspendLayout();
            // 
            // timerCheckDone
            // 
            this.timerCheckDone.Enabled = true;
            this.timerCheckDone.Tick += new System.EventHandler(this.timerCheckDone_Tick);
            // 
            // timerStatus
            // 
            this.timerStatus.Enabled = true;
            this.timerStatus.Interval = 1000;
            this.timerStatus.Tick += new System.EventHandler(this.timerStatus_Tick);
            // 
            // timerLearnMode
            // 
            this.timerLearnMode.Enabled = true;
            this.timerLearnMode.Interval = 1000;
            this.timerLearnMode.Tick += new System.EventHandler(this.timerLearnMode_Tick);
            // 
            // pictureBoxTop
            // 
            this.pictureBoxTop.Location = new System.Drawing.Point(0, 0);
            this.pictureBoxTop.Name = "pictureBoxTop";
            this.pictureBoxTop.Size = new System.Drawing.Size(713, 639);
            this.pictureBoxTop.TabIndex = 0;
            this.pictureBoxTop.TabStop = false;
            // 
            // ChessPuzzleSimulatorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(713, 639);
            this.Controls.Add(this.pictureBoxTop);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "ChessBoardViewer";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "ChessBoardViewer";
            this.Move += new System.EventHandler(this.ChessBoardViewer_Move);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxTop)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Timer timerCheckDone;
        private System.Windows.Forms.Timer timerStatus;
        private System.Windows.Forms.Timer timerLearnMode;
        private System.Windows.Forms.PictureBox pictureBoxTop;
    }
}