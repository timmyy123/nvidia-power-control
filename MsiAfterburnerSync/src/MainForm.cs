using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace MsiAfterburnerSync
{
    public sealed class MainForm : Form
    {
        private SyncSettings _settings;
        private MsiWatcher _watcher;

        private Label _lblStatus;
        private ComboBox _cmbProfile;
        private Label _lblProfileDetails;
        private TrackBar _tbDelay;
        private Label _lblDelayValue;
        private TextBox _txtAbPath;
        private CheckBox _chkAutoStart;
        private CheckBox _chkApplyOnStartup;
        private CheckBox _chkCloseToTray;
        private CheckBox _chkNotify;
        private CheckBox _chkApplyPowerUnlock;
        private NumericUpDown _nudPowerTarget;
        private TextBox _txtNvpwrPath;
        private Button _btnBrowseNvpwr;
        private Button _btnCleanNvpwrTask;
        private Button _btnTestApply;
        private Button _btnSave;
        private TextBox _txtLog;

        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private bool _forceExit;
        private EventWaitHandle _showWindowEvent;
        private Thread _showWindowThread;
        private readonly SynchronizationContext _uiContext;

        public SyncAppContext AppContext { get; set; }

        public MainForm()
        {
            _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            _settings = SettingsStore.Load();

            if (string.IsNullOrEmpty(_settings.AfterburnerPath) || !File.Exists(_settings.AfterburnerPath))
            {
                _settings.AfterburnerPath = AfterburnerController.FindAfterburnerPath();
            }

            Text = "MSI Afterburner & Power Limit Sync";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(700, 800);
            MinimumSize = new Size(620, 650);
            BackColor = Color.FromArgb(17, 19, 23);
            ForeColor = Color.FromArgb(235, 238, 242);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);

            try
            {
                Icon appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (appIcon != null) this.Icon = appIcon;
            }
            catch { }

            // Ensure window handle is created on the main UI thread immediately
            IntPtr forceHandle = this.Handle;

            BuildUi();
            InitTrayIcon();
            StartShowWindowListener();

            try
            {
                Microsoft.Win32.SystemEvents.SessionEnding += OnSessionEnding;
            }
            catch { }

            _watcher = new MsiWatcher(OnMsiScenarioChanged, Log);
            _watcher.TimeoutSec = _settings.DelaySec;
            _watcher.Start();

            Log("Started. Listening for MSI Center scenario change events (0% CPU).");

            if (_settings.ApplyOnStartup)
            {
                new Thread(() =>
                {
                    try
                    {
                        // Wait user configured delay (1-10s) after logon, then apply once
                        int waitMs = Math.Max(1000, Math.Min(10000, _settings.DelaySec * 1000));
                        Thread.Sleep(waitMs);

                        // If Afterburner is still starting up, wait briefly for its process to appear so it doesn't get missed
                        for (int i = 0; i < 20; i++)
                        {
                            if (Process.GetProcessesByName("MSIAfterburner").Length > 0) break;
                            Thread.Sleep(500);
                        }

                        if (!_forceExit)
                        {
                            ApplyAfterburnerProfile("Startup auto-apply");
                        }
                    }
                    catch { }
                })
                {
                    IsBackground = true,
                    Name = "StartupApplyThread"
                }.Start();
            }

            Resize += delegate
            {
                if (WindowState == FormWindowState.Minimized && _settings.CloseToTray)
                {
                    Hide();
                }
            };

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                _forceExit = true;
                if (_showWindowEvent != null)
                {
                    try { _showWindowEvent.Close(); } catch { }
                    _showWindowEvent = null;
                }
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                    _trayIcon = null;
                }
                if (_watcher != null)
                {
                    _watcher.Dispose();
                    _watcher = null;
                }
                try
                {
                    Microsoft.Win32.SystemEvents.SessionEnding -= OnSessionEnding;
                }
                catch { }
                SaveUiSettings();
                Application.Exit();
                Environment.Exit(0);
            };
        }

        private void OnSessionEnding(object sender, Microsoft.Win32.SessionEndingEventArgs e)
        {
            _forceExit = true;
            if (_watcher != null)
            {
                try { _watcher.Dispose(); } catch { }
                _watcher = null;
            }
            Environment.Exit(0);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_QUERYENDSESSION = 0x0011;
            const int WM_ENDSESSION = 0x0016;
            if (m.Msg == WM_QUERYENDSESSION || m.Msg == WM_ENDSESSION)
            {
                _forceExit = true;
                if (_watcher != null)
                {
                    try { _watcher.Dispose(); } catch { }
                    _watcher = null;
                }
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        public void RestoreWindow()
        {
            if (_uiContext != null && SynchronizationContext.Current != _uiContext)
            {
                _uiContext.Post(_ => RestoreWindow(), null);
                return;
            }

            Log("RestoreWindow triggered: showing UI (Handle=" + Handle + ", Visible=" + Visible + ", State=" + WindowState + ").");
            if (!Visible)
            {
                Show();
            }
            WindowState = FormWindowState.Normal;
            Log("After Show(): Handle=" + Handle + ", Visible=" + Visible + ", State=" + WindowState);
            BringToFront();
            Activate();
            try
            {
                ShowWindow(Handle, 9); // SW_RESTORE = 9
                ShowWindow(Handle, 5); // SW_SHOW = 5
                BringWindowToTop(Handle);
                SetForegroundWindow(Handle);
            }
            catch (Exception ex)
            {
                Log("ShowWindow Win32 error: " + ex.Message);
            }
        }

        private void StartShowWindowListener()
        {
            try
            {
                bool createdNew;
                System.Security.AccessControl.EventWaitHandleSecurity sec = new System.Security.AccessControl.EventWaitHandleSecurity();
                System.Security.AccessControl.EventWaitHandleAccessRule rule = new System.Security.AccessControl.EventWaitHandleAccessRule(
                    new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.WorldSid, null),
                    System.Security.AccessControl.EventWaitHandleRights.FullControl,
                    System.Security.AccessControl.AccessControlType.Allow);
                sec.AddAccessRule(rule);

                _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Global\\MsiAfterburnerSync_ShowWindow", out createdNew, sec);
                _showWindowThread = new Thread(() =>
                {
                    while (!_forceExit)
                    {
                        try
                        {
                            if (_showWindowEvent != null && _showWindowEvent.WaitOne(1000))
                            {
                                if (_forceExit) break;
                                if (_uiContext != null)
                                {
                                    _uiContext.Post(_ => RestoreWindow(), null);
                                }
                                else
                                {
                                    RestoreWindow();
                                }
                            }
                        }
                        catch { break; }
                    }
                })
                {
                    IsBackground = true
                };
                _showWindowThread.Start();
            }
            catch { }
        }

        private void BuildUi()
        {
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 8,
                Padding = new Padding(18)
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            Controls.Add(mainLayout);

            // 1. Header
            Panel pnlHeader = new Panel { Height = 56, Dock = DockStyle.Top };
            Label title = new Label
            {
                Text = "MSI Afterburner Overclock Sync",
                Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(0, 0),
                AutoSize = true
            };
            Label subTitle = new Label
            {
                Text = "Standalone, anti-cheat safe tool to keep Afterburner overclock active across MSI Center power mode switches",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(145, 155, 170),
                Location = new Point(0, 28),
                AutoSize = true
            };
            pnlHeader.Controls.AddRange(new Control[] { title, subTitle });
            mainLayout.Controls.Add(pnlHeader);

            // 2. Status Banner
            Panel pnlStatus = new Panel
            {
                Height = 36,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(19, 36, 26),
                Padding = new Padding(10, 8, 10, 8)
            };
            _lblStatus = new Label
            {
                Text = "● Active: Listening for MSI Center power mode switches (0% CPU)",
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Color.FromArgb(60, 210, 120),
                Dock = DockStyle.Fill
            };
            pnlStatus.Controls.Add(_lblStatus);
            mainLayout.Controls.Add(pnlStatus);

            // 3. Settings Card
            Panel pnlCard = new Panel
            {
                Height = 275,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(25, 29, 36),
                Padding = new Padding(16)
            };

            // Profile Slot
            Label lblProf = new Label { Text = "MSI Afterburner Profile Slot:", Location = new Point(16, 16), AutoSize = true, ForeColor = Color.White };
            _cmbProfile = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(230, 13),
                Width = 110,
                BackColor = Color.FromArgb(36, 42, 52),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            for (int i = 1; i <= 5; i++) _cmbProfile.Items.Add("Profile " + i);
            _cmbProfile.SelectedIndex = Math.Max(0, Math.Min(4, _settings.ProfileSlot - 1));

            _lblProfileDetails = new Label
            {
                Location = new Point(355, 16),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 200, 255),
                Font = new Font("Segoe UI Semibold", 9.5f)
            };
            UpdateProfileDetailsLabel();

            _cmbProfile.SelectedIndexChanged += delegate
            {
                _settings.ProfileSlot = _cmbProfile.SelectedIndex + 1;
                UpdateProfileDetailsLabel();
            };

            // Delay Slider
            Label lblDelay = new Label { Text = "Re-apply Delay (Timeout):", Location = new Point(16, 56), AutoSize = true, ForeColor = Color.White };
            _tbDelay = new TrackBar
            {
                Minimum = 1,
                Maximum = 10,
                Value = Math.Max(1, Math.Min(10, _settings.DelaySec)),
                TickFrequency = 1,
                Location = new Point(225, 52),
                Width = 260
            };
            _lblDelayValue = new Label
            {
                Text = _settings.DelaySec + " second" + (_settings.DelaySec > 1 ? "s" : ""),
                Location = new Point(495, 56),
                AutoSize = true,
                ForeColor = Color.FromArgb(255, 195, 60),
                Font = new Font("Segoe UI Semibold", 9.5f)
            };
            _tbDelay.ValueChanged += delegate
            {
                _settings.DelaySec = _tbDelay.Value;
                _lblDelayValue.Text = _tbDelay.Value + " second" + (_tbDelay.Value > 1 ? "s" : "");
                if (_watcher != null) _watcher.TimeoutSec = _tbDelay.Value;
            };

            // Path to Afterburner
            Label lblPath = new Label { Text = "MSI Afterburner Path:", Location = new Point(16, 100), AutoSize = true, ForeColor = Color.White };
            _txtAbPath = new TextBox
            {
                Text = _settings.AfterburnerPath,
                Location = new Point(16, 122),
                Width = 520,
                BackColor = Color.FromArgb(36, 42, 52),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Button btnBrowseAb = new Button
            {
                Text = "Browse…",
                Location = new Point(544, 120),
                Width = 90,
                Height = 27,
                BackColor = Color.FromArgb(46, 54, 68),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnBrowseAb.FlatAppearance.BorderSize = 0;
            btnBrowseAb.Click += delegate
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "MSIAfterburner.exe|MSIAfterburner.exe|Executable (*.exe)|*.exe";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        _txtAbPath.Text = ofd.FileName;
                        _settings.AfterburnerPath = ofd.FileName;
                        UpdateProfileDetailsLabel();
                    }
                }
            };

            // Power Unlock Section
            _chkApplyPowerUnlock = new CheckBox
            {
                Text = "Re-apply GPU Power Unlock on scenario switch:",
                Checked = _settings.ApplyPowerUnlock,
                Location = new Point(16, 158),
                AutoSize = true,
                ForeColor = Color.FromArgb(220, 225, 232),
                Font = new Font("Segoe UI Semibold", 9.5f)
            };
            _chkApplyPowerUnlock.CheckedChanged += delegate
            {
                _settings.ApplyPowerUnlock = _chkApplyPowerUnlock.Checked;
            };

            _nudPowerTarget = new NumericUpDown
            {
                Minimum = 100,
                Maximum = 400,
                Value = Math.Max(100, Math.Min(400, _settings.PowerTargetWatts)),
                Increment = 5,
                Location = new Point(345, 156),
                Width = 70,
                BackColor = Color.FromArgb(36, 42, 52),
                ForeColor = Color.White
            };
            Label lblWatts = new Label { Text = "Watts (e.g. 250W)", Location = new Point(420, 158), AutoSize = true, ForeColor = Color.FromArgb(255, 195, 60), Font = new Font("Segoe UI Semibold", 9.5f) };

            Label lblNvpwr = new Label { Text = "NvpwrControl CLI (used on-demand for 1-second power apply only):", Location = new Point(16, 192), AutoSize = true, ForeColor = Color.FromArgb(170, 180, 195) };
            _txtNvpwrPath = new TextBox
            {
                Text = _settings.NvpwrControlPath,
                Location = new Point(16, 214),
                Width = 520,
                BackColor = Color.FromArgb(36, 42, 52),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _btnBrowseNvpwr = new Button
            {
                Text = "Browse…",
                Location = new Point(544, 212),
                Width = 90,
                Height = 27,
                BackColor = Color.FromArgb(46, 54, 68),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnBrowseNvpwr.FlatAppearance.BorderSize = 0;
            _btnBrowseNvpwr.Click += delegate
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "NvpwrControl.exe|NvpwrControl.exe|Executable (*.exe)|*.exe";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        _txtNvpwrPath.Text = ofd.FileName;
                        _settings.NvpwrControlPath = ofd.FileName;
                    }
                }
            };

            // Options Checkboxes
            _chkAutoStart = new CheckBox
            {
                Text = "Start MsiAfterburnerSync with Windows at logon (Task Scheduler, 0 UAC prompts)",
                Checked = TaskSchedulerHelper.IsTaskInstalled(),
                Location = new Point(16, 252),
                AutoSize = true,
                ForeColor = Color.FromArgb(220, 225, 232)
            };
            _chkApplyOnStartup = new CheckBox
            {
                Text = "Apply overclock and power unlock once on Windows startup / application launch",
                Checked = _settings.ApplyOnStartup,
                Location = new Point(16, 278),
                AutoSize = true,
                ForeColor = Color.FromArgb(255, 205, 90),
                Font = new Font("Segoe UI Semibold", 9.5f)
            };
            _chkCloseToTray = new CheckBox
            {
                Text = "Minimize to system tray on close (run silently in background)",
                Checked = _settings.CloseToTray,
                Location = new Point(16, 304),
                AutoSize = true,
                ForeColor = Color.FromArgb(220, 225, 232)
            };
            _chkNotify = new CheckBox
            {
                Text = "Show notification balloon when overclock profile and power unlock are reapplied",
                Checked = _settings.NotifyOnApply,
                Location = new Point(16, 330),
                AutoSize = true,
                ForeColor = Color.FromArgb(220, 225, 232)
            };

            _btnCleanNvpwrTask = new Button
            {
                Text = "Remove NvpwrControl Windows Autostart Task",
                Location = new Point(16, 362),
                Width = 320,
                Height = 28,
                BackColor = Color.FromArgb(50, 40, 45),
                ForeColor = Color.FromArgb(255, 120, 120),
                FlatStyle = FlatStyle.Flat
            };
            _btnCleanNvpwrTask.FlatAppearance.BorderSize = 0;
            _btnCleanNvpwrTask.Click += delegate
            {
                string err;
                if (TaskSchedulerHelper.DeleteNvpwrControlTask(out err))
                {
                    Log("Successfully deleted NvpwrControlBlackwell Windows autostart task.");
                    MessageBox.Show("NvpwrControlBlackwell scheduled task removed successfully! NvpwrControl will never start with Windows.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    Log("Failed to delete NvpwrControlBlackwell: " + err);
                    MessageBox.Show("Could not remove task: " + err, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            pnlCard.Height = 405;
            pnlCard.Controls.AddRange(new Control[]
            {
                lblProf, _cmbProfile, _lblProfileDetails,
                lblDelay, _tbDelay, _lblDelayValue,
                lblPath, _txtAbPath, btnBrowseAb,
                _chkApplyPowerUnlock, _nudPowerTarget, lblWatts,
                lblNvpwr, _txtNvpwrPath, _btnBrowseNvpwr,
                _chkAutoStart, _chkApplyOnStartup, _chkCloseToTray, _chkNotify,
                _btnCleanNvpwrTask
            });
            mainLayout.Controls.Add(pnlCard);

            // 4. Buttons Row
            Panel pnlButtons = new Panel { Height = 45, Dock = DockStyle.Top };
            _btnTestApply = new Button
            {
                Text = "▶ Apply Profile Now (Test)",
                Location = new Point(0, 6),
                Width = 210,
                Height = 34,
                BackColor = Color.FromArgb(54, 123, 240),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f)
            };
            _btnTestApply.FlatAppearance.BorderSize = 0;
            _btnTestApply.Click += delegate { ApplyAfterburnerProfile("Manual user test"); };

            _btnSave = new Button
            {
                Text = "Save Settings",
                Location = new Point(220, 6),
                Width = 140,
                Height = 34,
                BackColor = Color.FromArgb(36, 140, 75),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f)
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += delegate
            {
                SaveUiSettings();
                MessageBox.Show("Settings saved successfully.", "MSI Afterburner Sync", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            pnlButtons.Controls.AddRange(new Control[] { _btnTestApply, _btnSave });
            mainLayout.Controls.Add(pnlButtons);

            // 5. Activity Log Label
            Label lblLog = new Label
            {
                Text = "Activity Log:",
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Color.FromArgb(170, 180, 195),
                Location = new Point(0, 8),
                AutoSize = true
            };
            Panel pnlLogHeader = new Panel { Height = 26, Dock = DockStyle.Top };
            pnlLogHeader.Controls.Add(lblLog);
            mainLayout.Controls.Add(pnlLogHeader);

            // 6. Activity Log Box
            _txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(12, 14, 18),
                ForeColor = Color.FromArgb(190, 205, 220),
                Font = new Font("Consolas", 9f),
                BorderStyle = BorderStyle.FixedSingle
            };
            mainLayout.Controls.Add(_txtLog);
        }

        private void UpdateProfileDetailsLabel()
        {
            if (_lblProfileDetails == null) return;
            string details = AfterburnerController.GetProfileDetails(_settings.AfterburnerPath, _settings.ProfileSlot);
            _lblProfileDetails.Text = details;
        }

        private void SaveUiSettings()
        {
            _settings.ProfileSlot = _cmbProfile.SelectedIndex + 1;
            _settings.DelaySec = _tbDelay.Value;
            _settings.AfterburnerPath = _txtAbPath.Text.Trim();
            _settings.AutoStart = _chkAutoStart.Checked;
            _settings.CloseToTray = _chkCloseToTray.Checked;
            _settings.NotifyOnApply = _chkNotify.Checked;
            _settings.ApplyPowerUnlock = _chkApplyPowerUnlock.Checked;
            _settings.PowerTargetWatts = (int)_nudPowerTarget.Value;
            _settings.NvpwrControlPath = _txtNvpwrPath.Text.Trim();
            _settings.ApplyOnStartup = _chkApplyOnStartup.Checked;

            if (_watcher != null) _watcher.TimeoutSec = _settings.DelaySec;

            // Handle Task Scheduler autostart
            if (_settings.AutoStart)
            {
                if (!TaskSchedulerHelper.IsTaskInstalled())
                {
                    string err;
                    if (TaskSchedulerHelper.InstallTask(Application.ExecutablePath, out err))
                    {
                        Log("Autostart Scheduled Task registered (highest privileges, 0 UAC).");
                    }
                    else
                    {
                        Log("Failed to register Scheduled Task: " + err);
                    }
                }
            }
            else
            {
                if (TaskSchedulerHelper.IsTaskInstalled())
                {
                    string err;
                    TaskSchedulerHelper.RemoveTask(out err);
                    Log("Autostart Scheduled Task removed.");
                }
            }

            SettingsStore.Save(_settings);
        }

        private void InitTrayIcon()
        {
            // Tray icon strictly disabled - user requested zero tray icon
            _trayIcon = null;
        }

        private void OnMsiScenarioChanged()
        {
            ApplyAfterburnerProfile("MSI Center scenario switch");
        }

        private void ApplyAfterburnerProfile(string triggerReason)
        {
            if (PowerUnlockController.IsAntiCheatGameRunning())
            {
                Log("[" + triggerReason + "] Anti-cheat protected game is active. Skipping GPU hook calls to protect game integrity.");
                return;
            }

            // 1. Re-apply Power Unlock (if enabled)
            string powerResultMsg = "";
            bool powerOk = true;
            if (_settings.ApplyPowerUnlock)
            {
                string pMsg;
                powerOk = PowerUnlockController.ApplyPowerLimit(_settings.PowerTargetWatts, out pMsg);
                powerResultMsg = " | Power: " + (powerOk ? (_settings.PowerTargetWatts + "W OK") : ("FAIL: " + pMsg));
                Log("[" + triggerReason + "] Power Unlock (" + _settings.PowerTargetWatts + "W) -> " + (powerOk ? "SUCCESS" : "FAIL: " + pMsg));
            }

            // 2. Direct Hardware Overclock via NVAPI (+240 Core, +1100 Mem)
            int coreMhz = _settings.CoreOffsetMHz;
            int memMhz = _settings.MemoryOffsetMHz;
            if (coreMhz != 0 || memMhz != 0)
            {
                string ocMsg;
                bool ocOk = PowerUnlockController.ApplyOverclock(coreMhz, memMhz, out ocMsg);
                Log("[" + triggerReason + "] Hardware OC (Core " + (coreMhz >= 0 ? "+" : "") + coreMhz + " MHz, Mem " + (memMhz >= 0 ? "+" : "") + memMhz + " MHz) -> " + (ocOk ? "SUCCESS" : "FAIL: " + ocMsg));
            }

            // 3. Re-apply MSI Afterburner OC Profile (if path exists)
            if (!string.IsNullOrEmpty(_settings.AfterburnerPath) && File.Exists(_settings.AfterburnerPath))
            {
                string msg;
                bool ok = AfterburnerController.ApplyProfile(_settings.AfterburnerPath, _settings.ProfileSlot, out msg);
                Log("[" + triggerReason + "] Profile " + _settings.ProfileSlot + " -> " + (ok ? "SUCCESS" : "FAIL: " + msg));
            }
        }

        private void Log(string message)
        {
            string nowIso = DateTime.Now.ToString("o");
            string nowShort = DateTime.Now.ToString("HH:mm:ss.fff");
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MsiAfterburnerSync");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "sync.log");
                File.AppendAllText(file, "[" + nowIso + "] " + message + Environment.NewLine);
            }
            catch { }

            if (_uiContext != null)
            {
                _uiContext.Post(delegate
                {
                    if (_txtLog != null && !_txtLog.IsDisposed)
                    {
                        _txtLog.AppendText("[" + nowShort + "] " + message + Environment.NewLine);
                    }
                }, null);
            }
        }
    }
}
