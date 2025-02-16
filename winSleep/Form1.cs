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
        private readonly Timer activityTimer = new Timer();
        private DateTime lastActivityTime;
        private NotifyIcon trayIcon;
        private int sleepThreshold = 1800; // 預設30分鐘
        private int warningSeconds = 30;   // 新增：預設30秒的警告時間
        private ToolStripMenuItem countdownItem;
        private KeyboardHook keyboardHook;
        private bool isPaused = false;  // 新增：用於追蹤暫停狀態
        private ToolStripMenuItem pauseItem;  // 新增：暫停選單項目

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
            InitializeApplication();
        }

        private void InitializeApplication()
        {
            // 設定系統托盤圖示
            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "自動休眠控制器",
                Visible = true
            };

            // 建立右鍵選單
            var contextMenu = new ContextMenuStrip();
            countdownItem = new ToolStripMenuItem("剩餘時間: --:--") { Enabled = false };
            contextMenu.Items.Add(countdownItem);
            contextMenu.Items.Add("-");
            
            // 新增：暫停/繼續選單項目
            pauseItem = new ToolStripMenuItem("暫停計時", null, OnPauseClick);
            contextMenu.Items.Add(pauseItem);
            
            contextMenu.Items.Add("設定時間", null, OnSettingsClick);
            contextMenu.Items.Add("退出", null, OnExitClick);
            trayIcon.ContextMenuStrip = contextMenu;

            // 初始化計時器
            activityTimer.Interval = 1000;
            activityTimer.Tick += CheckActivity;
            activityTimer.Start();

            // 初始化鍵盤鉤子
            keyboardHook = new KeyboardHook(this);

            lastActivityTime = DateTime.Now;
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (keyboardHook != null)
            {
                keyboardHook.Dispose();
            }
        }

        public void UpdateLastActivityTime()
        {
            // 如果暫停中，不更新時間
            if (isPaused)
                return;

            System.Diagnostics.Debug.WriteLine("Activity detected, resetting timer");
            lastActivityTime = DateTime.Now;
            if (!activityTimer.Enabled)
            {
                activityTimer.Start();
            }
            
            // 更新托盤圖示顯示
            var remainingTime = TimeSpan.FromSeconds(sleepThreshold);
            countdownItem.Text = $"進入休眠倒數: {remainingTime.Minutes:D2}:{remainingTime.Seconds:D2}";
            trayIcon.Text = $"自動休眠控制器\n剩餘: {remainingTime.Minutes:D2}:{remainingTime.Seconds:D2}";
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // 移除這行，因為我們不再使用 StartActivityMonitoring
            // StartActivityMonitoring();
        }

        private void CheckActivity(object sender, EventArgs e)
        {
            // 如果暫停中，不執行檢查
            if (isPaused)
                return;

            var inactiveTime = DateTime.Now - lastActivityTime;
            var remainingTime = TimeSpan.FromSeconds(sleepThreshold) - inactiveTime;

            // 更新倒數時間顯示
            if (remainingTime.TotalSeconds > 0)
            {
                countdownItem.Text = $"進入休眠倒數: {remainingTime.Minutes:D2}:{remainingTime.Seconds:D2}";
                trayIcon.Text = $"自動休眠控制器\n剩餘: {remainingTime.Minutes:D2}:{remainingTime.Seconds:D2}";
            }

            // 檢查是否需要進入休眠
            if (inactiveTime.TotalSeconds >= sleepThreshold)
            {
                // 停止計時器
                activityTimer.Stop();

                // 建立一個自動關閉的訊息框
                var autoCloseForm = new Form
                {
                    Size = new Size(400, 150),
                    Text = "自動休眠控制器",
                    StartPosition = FormStartPosition.CenterScreen,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    TopMost = true
                };

                var messageLabel = new Label
                {
                    Text = $"系統將在 {warningSeconds} 秒後進入休眠模式\n已經 {sleepThreshold/60} 分 {sleepThreshold%60} 秒沒有鍵盤輸入\n\n按「取消」可以取消休眠",
                    AutoSize = true,
                    Location = new Point(20, 20)
                };

                var cancelButton = new Button
                {
                    Text = "取消",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(150, 80),
                    Width = 80
                };

                var countdownLabel = new Label
                {
                    Text = "5",
                    Location = new Point(20, 80),
                    AutoSize = true
                };

                autoCloseForm.Controls.AddRange(new Control[] { messageLabel, cancelButton, countdownLabel });
                autoCloseForm.CancelButton = cancelButton;

                // 修改倒數計時
                var countDown = warningSeconds;  // 使用設定的警告時間
                var countDownTimer = new Timer { Interval = 1000 };
                countDownTimer.Tick += (s, ev) =>
                {
                    countDown--;
                    countdownLabel.Text = countDown.ToString();
                    if (countDown <= 0)
                    {
                        countDownTimer.Stop();
                        autoCloseForm.DialogResult = DialogResult.OK;
                    }
                };
                countDownTimer.Start();

                // 顯示對話框
                var result = autoCloseForm.ShowDialog();

                // 清理資源
                countDownTimer.Dispose();
                autoCloseForm.Dispose();

                if (result == DialogResult.Cancel)
                {
                    // 如果使用者取消，重置計時器
                    lastActivityTime = DateTime.Now;
                    activityTimer.Start();
                    return;
                }

                // 執行休眠指令前停止計時器
                activityTimer.Stop();
                
                // 執行休眠指令
                Process.Start("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");

                // 等待一段時間確保系統有時間進入休眠
                System.Threading.Thread.Sleep(2000);
            }
        }

        private void OnSettingsClick(object sender, EventArgs e)
        {
            using (var dialog = new Form())
            {
                dialog.Text = "設定時間";
                dialog.Width = 180;
                
                // 休眠時間標籤
                var timeLabel = new Label
                {
                    Text = "休眠時間:",
                    Location = new Point(10, 10),
                    AutoSize = true
                };

                // 分鐘設定
                var minutesUpDown = new NumericUpDown
                {
                    Minimum = 0,
                    Maximum = 120,
                    Value = sleepThreshold / 60,
                    Location = new Point(10, timeLabel.Bottom + 5),
                    Width = 60
                };

                // 秒數設定
                var secondsUpDown = new NumericUpDown
                {
                    Minimum = 0,
                    Maximum = 59,
                    Value = sleepThreshold % 60,
                    Location = new Point(minutesUpDown.Right + 5, minutesUpDown.Top),
                    Width = 60
                };

                // 新增：警告時間標籤
                var warningLabel = new Label
                {
                    Text = "警告時間(秒):",
                    Location = new Point(10, minutesUpDown.Bottom + 15),
                    AutoSize = true
                };

                // 新增：警告時間設定
                var warningUpDown = new NumericUpDown
                {
                    Minimum = 5,
                    Maximum = 120,
                    Value = warningSeconds,
                    Location = new Point(10, warningLabel.Bottom + 5),
                    Width = 60
                };

                var minutesLabel = new Label
                {
                    Text = "分",
                    Location = new Point(minutesUpDown.Right - 15, minutesUpDown.Top + 2),
                    AutoSize = true
                };

                var secondsLabel = new Label
                {
                    Text = "秒",
                    Location = new Point(secondsUpDown.Right - 15, secondsUpDown.Top + 2),
                    AutoSize = true
                };

                // 確定按鈕
                var button = new Button
                {
                    Text = "確定",
                    DialogResult = DialogResult.OK,
                    Location = new Point((dialog.ClientSize.Width - 60) / 2, warningUpDown.Bottom + 10),
                    Width = 60
                };

                dialog.Controls.AddRange(new Control[] { 
                    timeLabel, 
                    minutesUpDown, minutesLabel,
                    secondsUpDown, secondsLabel,
                    warningLabel, warningUpDown,
                    button 
                });
                
                dialog.Height = button.Bottom + 35;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterScreen;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    sleepThreshold = (int)(minutesUpDown.Value * 60 + secondsUpDown.Value);
                    warningSeconds = (int)warningUpDown.Value;  // 儲存警告時間
                    lastActivityTime = DateTime.Now;
                }
            }
        }

        private void OnExitClick(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }

        // 新增：處理暫停/繼續點擊事件
        private void OnPauseClick(object sender, EventArgs e)
        {
            isPaused = !isPaused;
            if (isPaused)
            {
                activityTimer.Stop();
                pauseItem.Text = "繼續計時";
                trayIcon.Text = "自動休眠控制器 (已暫停)";
                countdownItem.Text = "計時已暫停";
            }
            else
            {
                lastActivityTime = DateTime.Now;  // 重置計時器
                activityTimer.Start();
                pauseItem.Text = "暫停計時";
                UpdateLastActivityTime();  // 更新顯示
            }
        }

        // 新增：系統恢復事件處理
        protected override void WndProc(ref Message m)
        {
            const int WM_POWERBROADCAST = 0x0218;
            const int PBT_APMRESUMEAUTOMATIC = 0x0012;

            if (m.Msg == WM_POWERBROADCAST && (int)m.WParam == PBT_APMRESUMEAUTOMATIC)
            {
                // 系統從休眠恢復時重置時間和啟動計時器
                lastActivityTime = DateTime.Now;
                if (!activityTimer.Enabled)
                {
                    activityTimer.Start();
                }
            }
            base.WndProc(ref m);
        }
    }
}
