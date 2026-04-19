using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace winSleep
{
    public partial class Form1 : Form
    {
        private const string APP_VERSION = "v1.1.20260419";
        private readonly Timer activityTimer = new Timer();
        private DateTime lastActivityTime;
        private NotifyIcon trayIcon;
        private int sleepThreshold = 1800; // 預設 30 分鐘 (1800秒)
        private int warningSeconds = 60;   // 將告警倒數改為 1 分鐘 (60秒)
        private ToolStripMenuItem countdownItem;
        private KeyboardHook keyboardHook;
        private MouseHook mouseHook;
        private bool isPaused;
        private ToolStripMenuItem pauseItem;
        private ToolStripMenuItem sleepNowItem;
        private Form autoCloseForm;

        [DllImport("user32.dll")]
        static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        [DllImport("user32.dll")]
        private static extern int SendMessage(int hWnd, int hMsg, int wParam, int lParam);
        private const int SC_MONITORPOWER = 0xF170;
        private const int WM_SYSCOMMAND = 0x112;
        private const int MONITOR_OFF = 2;

        public Form1()
        {
            InitializeComponent();
            isPaused = false;
            InitializeApplication();
        }

        private void InitializeApplication()
        {
            trayIcon = new NotifyIcon();
            trayIcon.Icon = SystemIcons.Application;
            trayIcon.Text = string.Format("自動休眠控制器 {0}", APP_VERSION);
            trayIcon.Visible = true;

            ContextMenuStrip contextMenu = new ContextMenuStrip();
            countdownItem = new ToolStripMenuItem("剩餘時間: --:--") { Enabled = false };
            contextMenu.Items.Add(countdownItem);
            contextMenu.Items.Add("-");
            
            pauseItem = new ToolStripMenuItem("暫停計時", null, OnPauseClick);
            contextMenu.Items.Add(pauseItem);

            sleepNowItem = new ToolStripMenuItem("立即休眠", null, OnSleepNowClick);
            contextMenu.Items.Add(sleepNowItem);
            
            contextMenu.Items.Add("設定時間", null, OnSettingsClick);
            contextMenu.Items.Add("退出", null, OnExitClick);
            trayIcon.ContextMenuStrip = contextMenu;

            activityTimer.Interval = 1000;
            activityTimer.Tick += new EventHandler(CheckActivity);
            activityTimer.Start();

            keyboardHook = new KeyboardHook(this);
            mouseHook = new MouseHook(this);

            lastActivityTime = DateTime.Now;
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (keyboardHook != null) keyboardHook.Dispose();
            if (mouseHook != null) mouseHook.Dispose();
        }

        public void UpdateLastActivityTime()
        {
            if (isPaused) return;

            if (autoCloseForm != null && !autoCloseForm.IsDisposed && autoCloseForm.Visible)
            {
                if (autoCloseForm.InvokeRequired)
                {
                    autoCloseForm.Invoke((MethodInvoker)delegate {
                        autoCloseForm.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                        autoCloseForm.Close();
                    });
                }
                else
                {
                    autoCloseForm.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                    autoCloseForm.Close();
                }
            }

            lastActivityTime = DateTime.Now;
            if (!activityTimer.Enabled) activityTimer.Start();
            
            TimeSpan remainingTime = TimeSpan.FromSeconds(sleepThreshold);
            countdownItem.Text = string.Format("進入休眠倒數: {0:D2}:{1:D2}", remainingTime.Minutes, remainingTime.Seconds);
            trayIcon.Text = string.Format("自動休眠控制器 {0} | 剩餘: {1:D2}:{2:D2}", APP_VERSION, remainingTime.Minutes, remainingTime.Seconds);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
        }

        private void CheckActivity(object sender, EventArgs e)
        {
            if (isPaused) return;

            TimeSpan inactiveTime = DateTime.Now - lastActivityTime;
            TimeSpan remainingTime = TimeSpan.FromSeconds(sleepThreshold) - inactiveTime;

            if (remainingTime.TotalSeconds > 0)
            {
                countdownItem.Text = string.Format("進入休眠倒數: {0:D2}:{1:D2}", remainingTime.Minutes, remainingTime.Seconds);
                trayIcon.Text = string.Format("自動休眠控制器 {0} | 剩餘: {1:D2}:{2:D2}", APP_VERSION, remainingTime.Minutes, remainingTime.Seconds);
            }

            if (inactiveTime.TotalSeconds >= sleepThreshold)
            {
                TriggerCountdown();
            }
        }

        private void TriggerCountdown()
        {
            activityTimer.Stop();

            if (autoCloseForm != null && !autoCloseForm.IsDisposed)
            {
                autoCloseForm.Close();
            }

            autoCloseForm = new Form();
            autoCloseForm.Size = new Size(400, 150);
            autoCloseForm.Text = string.Format("自動休眠控制器 {0}", APP_VERSION);
            autoCloseForm.StartPosition = FormStartPosition.CenterScreen;
            autoCloseForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            autoCloseForm.MaximizeBox = false;
            autoCloseForm.MinimizeBox = false;
            autoCloseForm.TopMost = true;

            Label messageLabel = new Label();
            messageLabel.Text = string.Format("系統將在 {0} 秒後進入休眠模式\n已經 {1} 分 {2} 秒沒有鍵盤輸入\n\n按「取消」可以取消休眠", 
                                            warningSeconds, sleepThreshold / 60, sleepThreshold % 60);
            messageLabel.AutoSize = true;
            messageLabel.Location = new Point(20, 20);

            Button cancelButton = new Button();
            cancelButton.Text = "取消";
            cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            cancelButton.Location = new Point(150, 80);
            cancelButton.Width = 80;

            Label countdownLabel = new Label();
            countdownLabel.Text = warningSeconds.ToString();
            countdownLabel.Location = new Point(20, 80);
            countdownLabel.AutoSize = true;

            autoCloseForm.Controls.Add(messageLabel);
            autoCloseForm.Controls.Add(cancelButton);
            autoCloseForm.Controls.Add(countdownLabel);
            autoCloseForm.CancelButton = cancelButton;

            int countDown = warningSeconds;
            Timer countDownTimer = new Timer();
            countDownTimer.Interval = 1000;
            countDownTimer.Tick += (s, ev) =>
            {
                countDown--;
                if (!autoCloseForm.IsDisposed)
                {
                    countdownLabel.Text = countDown.ToString();
                }
                
                if (countDown <= 0)
                {
                    countDownTimer.Stop();
                    autoCloseForm.DialogResult = System.Windows.Forms.DialogResult.OK;
                }
            };
            countDownTimer.Start();

            System.Windows.Forms.DialogResult result = autoCloseForm.ShowDialog();
            countDownTimer.Dispose();
            autoCloseForm.Dispose();

            if (result == System.Windows.Forms.DialogResult.Cancel)
            {
                lastActivityTime = DateTime.Now;
                activityTimer.Start();
                return;
            }

            Process.Start("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
            System.Threading.Thread.Sleep(2000);
        }

        private void OnSleepNowClick(object sender, EventArgs e)
        {
            SleepNow();
        }

        private void SleepNow()
        {
            TriggerCountdown();
        }

        private void OnSettingsClick(object sender, EventArgs e)
        {
            using (Form dialog = new Form())
            {
                dialog.Text = "設定時間";
                dialog.Width = 200;
                
                Label timeLabel = new Label();
                timeLabel.Text = "休眠時間:";
                timeLabel.Location = new Point(10, 10);
                timeLabel.AutoSize = true;

                NumericUpDown minutesUpDown = new NumericUpDown();
                minutesUpDown.Minimum = 0;
                minutesUpDown.Maximum = 120;
                minutesUpDown.Value = sleepThreshold / 60;
                minutesUpDown.Location = new Point(10, 30);
                minutesUpDown.Width = 50;

                Label minText = new Label();
                minText.Text = "分";
                minText.Location = new Point(65, 32);
                minText.AutoSize = true;

                NumericUpDown secondsUpDown = new NumericUpDown();
                secondsUpDown.Minimum = 0;
                secondsUpDown.Maximum = 59;
                secondsUpDown.Value = sleepThreshold % 60;
                secondsUpDown.Location = new Point(90, 30);
                secondsUpDown.Width = 50;

                Label secText = new Label();
                secText.Text = "秒";
                secText.Location = new Point(145, 32);
                secText.AutoSize = true;

                Label warningLabel = new Label();
                warningLabel.Text = "警告時間(秒):";
                warningLabel.Location = new Point(10, 60);
                warningLabel.AutoSize = true;

                NumericUpDown warningUpDown = new NumericUpDown();
                warningUpDown.Minimum = 5;
                warningUpDown.Maximum = 120;
                warningUpDown.Value = (decimal)warningSeconds;
                warningUpDown.Location = new Point(10, 80);
                warningUpDown.Width = 60;

                Button button = new Button();
                button.Text = "確定";
                button.DialogResult = System.Windows.Forms.DialogResult.OK;
                button.Location = new Point(60, 115);
                button.Width = 60;

                dialog.Controls.Add(timeLabel);
                dialog.Controls.Add(minutesUpDown);
                dialog.Controls.Add(minText);
                dialog.Controls.Add(secondsUpDown);
                dialog.Controls.Add(secText);
                dialog.Controls.Add(warningLabel);
                dialog.Controls.Add(warningUpDown);
                dialog.Controls.Add(button);
                
                dialog.Height = 190;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterScreen;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    sleepThreshold = (int)(minutesUpDown.Value * 60 + secondsUpDown.Value);
                    warningSeconds = (int)warningUpDown.Value;
                    lastActivityTime = DateTime.Now;
                }
            }
        }

        private void OnExitClick(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }

        private void OnPauseClick(object sender, EventArgs e)
        {
            isPaused = !isPaused;
            if (isPaused)
            {
                activityTimer.Stop();
                pauseItem.Text = "繼續計時";
                trayIcon.Text = string.Format("自動休眠控制器 {0} (已暫停)", APP_VERSION);
                countdownItem.Text = "計時已暫停";
            }
            else
            {
                lastActivityTime = DateTime.Now;
                activityTimer.Start();
                pauseItem.Text = "暫停計時";
                UpdateLastActivityTime();
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_POWERBROADCAST = 0x0218;
            const int PBT_APMRESUMEAUTOMATIC = 0x0012;
            if (m.Msg == WM_POWERBROADCAST && (int)m.WParam == PBT_APMRESUMEAUTOMATIC)
            {
                lastActivityTime = DateTime.Now;
                if (!activityTimer.Enabled) activityTimer.Start();
            }
            base.WndProc(ref m);
        }
    }
}
