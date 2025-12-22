using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace GameHeatmap
{
    public class LoadingProgressForm : Form
    {
        private Label lblStatus;
        private ProgressBar progressBar;
        private Button btnCancel;
        private BackgroundWorker worker;
        
        public bool WasCancelled { get; private set; }
        
        public LoadingProgressForm()
        {
            InitializeComponent();
            WasCancelled = false;
        }
        
        private void InitializeComponent()
        {
            this.Text = "Loading Databases";
            this.Size = new Size(500, 180);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            lblStatus = new Label
            {
                Text = "Initializing...",
                Location = new Point(20, 20),
                Size = new Size(440, 25),
                Font = new Font("Segoe UI", 10)
            };
            this.Controls.Add(lblStatus);
            
            progressBar = new ProgressBar
            {
                Location = new Point(20, 55),
                Size = new Size(440, 30),
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };
            this.Controls.Add(progressBar);
            
            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(200, 100),
                Size = new Size(100, 30)
            };
            btnCancel.Click += BtnCancel_Click;
            this.Controls.Add(btnCancel);
            
            this.FormClosing += LoadingProgressForm_FormClosing;
        }
        
        private void BtnCancel_Click(object sender, EventArgs e)
        {
            WasCancelled = true;
            btnCancel.Enabled = false;
            btnCancel.Text = "Cancelling...";
            lblStatus.Text = "Cancelling...";
            
            if (worker != null && worker.IsBusy)
            {
                worker.CancelAsync();
            }
        }
        
        private void LoadingProgressForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Treat closing as cancel
            if (worker != null && worker.IsBusy)
            {
                WasCancelled = true;
                worker.CancelAsync();
                e.Cancel = true; // Prevent closing until worker completes
            }
        }
        
        public void SetStatus(string status)
        {
            if (lblStatus.InvokeRequired)
            {
                lblStatus.Invoke(new Action(() => lblStatus.Text = status));
            }
            else
            {
                lblStatus.Text = status;
            }
        }
        
        public void SetProgress(int percentage)
        {
            if (progressBar.InvokeRequired)
            {
                progressBar.Invoke(new Action(() => progressBar.Value = Math.Min(100, Math.Max(0, percentage))));
            }
            else
            {
                progressBar.Value = Math.Min(100, Math.Max(0, percentage));
            }
        }
        
        public void SetWorker(BackgroundWorker bgWorker)
        {
            worker = bgWorker;
        }
        
        public void Complete()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(Complete));
            }
            else
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }
    }
}
