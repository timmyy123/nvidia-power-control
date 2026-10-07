using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NvpwrControlBlackwell
{
    internal sealed class MainForm : Form
    {
        private readonly PowerBackend _power = new PowerBackend();
        private AppSettings _settings;
        private CompatibilityState _compat = new CompatibilityState();
        private TunerState _tuner = new TunerState();
        private bool _busy;
        private bool _telemetryBusy;
        private bool _autoVbiosAttempted;
        private string _lastAutoVbiosMessage = "";
        private double _userScale = 1.0;

        private Panel _header, _nav, _content;
        private Label _gpuName, _gpuSub, _compatBadge;
        private Label _headCore, _headVolt, _headPower, _headTemp, _headHotspot;
        private NavButton _navPower, _navTune, _navTelemetry, _navCompat, _navSettings;
        private Button _resetAll;
        private Panel _pagePower, _pageTune, _pageTelemetry, _pageCompat, _pageSettings;
        private Panel _rebootBanner;
        private Label _rebootText;
        private Button _rebootNow;

        private Label _powerCurrent, _powerMax, _powerVoltage, _powerOverride, _powerRange, _powerExperimental;
        private Label _maxTargetValue, _currentTargetValue, _voltTargetValue, _voltStatus;
        private PowerSlider _maxSlider, _currentSlider, _voltSlider;
        private Button _applyMax, _applyCurrent, _restoreCurrent, _restoreMax, _applyVolt, _restartDriverVolt, _restoreVolt;
        private Button _autostart, _removeAutostart;
        private Label _workflowText;

        private NumericUpDown _core, _mem, _xbar, _msvdd, _nvvdd, _ratio;
        private CheckBox _coreEnable, _memEnable, _xbarEnable, _msvddEnable, _nvvddEnable, _ratioEnable;
        private Label _coreRange, _memRange, _xbarRange, _msvddRange, _nvvddRange, _ratioRange, _tuneSummary;
        private Button _probeTune, _applyTune, _resetTune;

        private Label _monPower, _monTemp, _monHotspot, _monMemTemp, _monUtil, _monCore, _monMem, _monCurrent, _monMax;
        private Button _restartNv;

        private TextBox _compatText;
        private Label _compatGpuState, _compatDriverState, _compatVbiosState, _compatPolicyState;
        private Button _validateDriver, _autoVbios, _selectRom, _recheck, _exportReport, _openLog;

        private ComboBox _lang, _theme, _accent, _buttonAccent, _sliderAccent, _uiScale;
        private NumericUpDown _telemetryMs;
        private CheckBox _telemetryEnable;
        private CheckBox _telemetryTogglePage;
        private Button _telemetryRefreshBtn;
        private Label _telemetryStatusLabel;
        private CheckBox _closeToTray;
        private CheckBox _msiAutoApply;
        private MsiScenarioWatcher _msiWatcher;
        private PowerSlider _msiTimeoutSlider, _msiTimeoutSliderSettings;
        private Label _msiTimeoutLabel, _msiTimeoutValue;
        private Label _settingsNote;

        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private bool _startMinimized = false;
        private bool _forceExit = false;
        private EventWaitHandle _showWindowEvent;
        private Thread _showWindowThread;

        private StatusStrip _status;
        private ToolStripStatusLabel _statusText;
        private System.Windows.Forms.Timer _timer;

        private Color _bg, _card, _card2, _fg, _muted, _border, _accentColor, _buttonColor, _sliderColor, _danger, _ok, _warning;
        private readonly List<FlowLayoutPanel> _responsiveRoots = new List<FlowLayoutPanel>();
        private readonly List<TableLayoutPanel> _responsiveGrid2 = new List<TableLayoutPanel>();
        private readonly List<Control> _responsiveWide = new List<Control>();
        private readonly Dictionary<TableLayoutPanel, Dictionary<Control, TableLayoutPanelCellPosition>> _gridOriginalPositions = new Dictionary<TableLayoutPanel, Dictionary<Control, TableLayoutPanelCellPosition>>();
        private readonly Dictionary<TableLayoutPanel, bool> _gridSingleColumn = new Dictionary<TableLayoutPanel, bool>();
        private TableLayoutPanel _telemetryGrid;

        private static readonly Dictionary<string, string[]> Tr = BuildTranslations();

        public MainForm(bool startMinimized = false)
        {
            _startMinimized = startMinimized;
            _settings = SettingsStore.Load();
            _userScale = _settings.UiScalePercent / 100.0;
            _msiWatcher = new MsiScenarioWatcher(HandleMsiScenarioChanged);
            _msiWatcher.TimeoutSec = _settings.MsiTimeoutSec;

            Text = "NvpwrControl — Unified Ada & Blackwell";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(U(1040), U(720));
            Size = new Size(Math.Max(U(1040), _settings.WindowWidth), Math.Max(U(720), _settings.WindowHeight));
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", (float)(9.3 * _userScale), FontStyle.Regular, GraphicsUnit.Point);
            KeyPreview = true;

            try
            {
                Icon appIcon = this.Icon;
                if (appIcon == null)
                {
                    try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
                }
                if (appIcon != null) this.Icon = appIcon;
            }
            catch { }

            BuildUi();
            ApplyTheme();
            ApplyLanguage();
            ApplySettingsSelectors();
            ShowPage(_settings.LastPage);

            InitTrayIcon();
            StartShowWindowListener();

            Shown += delegate
            {
                if (_settings.WindowMaximized) WindowState = FormWindowState.Maximized;
                LayoutResponsivePages();
            };
            Resize += delegate
            {
                LayoutResponsivePages();
                if (WindowState == FormWindowState.Minimized)
                {
                    if (_timer != null) _timer.Stop();
                }
                else if (WindowState != FormWindowState.Minimized && Visible && _settings.TelemetryEnabled)
                {
                    if (_timer != null && !_timer.Enabled) _timer.Start();
                }
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                _forceExit = true;
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Dispose();
                    _timer = null;
                }
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
                SaveSettings();
                if (_msiWatcher != null)
                {
                    _msiWatcher.Dispose();
                    _msiWatcher = null;
                }
                try { Application.Exit(); } catch { }
                try { Environment.Exit(0); } catch { }
            };

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = Math.Max(500, _settings.TelemetryIntervalMs);
            _timer.Tick += delegate
            {
                if (_settings.TelemetryEnabled && Visible && WindowState != FormWindowState.Minimized)
                    RefreshTelemetryAsync();
            };
            if (_settings.TelemetryEnabled && !_startMinimized)
            {
                _timer.Start();
            }

            RefreshEverythingAsync();
            if (_settings.MsiAutoApply)
            {
                _msiWatcher.Start();
                EnsureMsiSyncRunning();
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            if (_startMinimized)
            {
                value = false;
                if (!IsHandleCreated) CreateHandle();
            }
            base.SetVisibleCore(value);
            if (!value)
            {
                if (_timer != null) _timer.Stop();
            }
            else if (_settings != null && _settings.TelemetryEnabled && _timer != null && WindowState != FormWindowState.Minimized)
            {
                _timer.Start();
            }
        }

        public void RestoreWindow()
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(RestoreWindow)); } catch { }
                return;
            }
            _startMinimized = false;
            Show();
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            LayoutResponsivePages();
            BringToFront();
            Activate();

            if (_settings.TelemetryEnabled)
            {
                if (_timer != null) _timer.Start();
                RefreshTelemetryAsync();
            }
        }

        private void StartShowWindowListener()
        {
            try
            {
                _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\NvpwrControl_ShowWindow_Event");
                _showWindowThread = new Thread(ShowWindowListenerLoop);
                _showWindowThread.IsBackground = true;
                _showWindowThread.Start();
            }
            catch (Exception ex)
            {
                AppLog.Write("StartShowWindowListener error: " + ex.Message);
            }
        }

        private void ShowWindowListenerLoop()
        {
            while (!_forceExit)
            {
                try
                {
                    if (_showWindowEvent != null && _showWindowEvent.WaitOne(1000))
                    {
                        if (_forceExit) break;
                        RestoreWindow();
                    }
                }
                catch { break; }
            }
        }

        private void InitTrayIcon()
        {
            // Tray icon disabled: app runs as a clean desktop window and never shows a tray icon.
            _trayIcon = null;
        }

        private void ReapplyAllAsync()
        {
            try
            {
                if (_compat == null || _compat.Profile == null || !_compat.CurrentWritesReady)
                {
                    try { _compat = _power.CheckCompatibility(); } catch { }
                }
                if (_compat == null || _compat.Profile == null || !_compat.CurrentWritesReady) return;

                int targetW = _settings.CurrentSelection > 0 ? _settings.CurrentSelection : 0;
                string statusMsg = "";
                if (targetW > 0)
                {
                    OperationResult r = _power.SetCurrent(targetW);
                    statusMsg = r.Success ? ("CURRENT " + targetW.ToString() + " W") : ("Power FAIL: " + r.Message);
                }

                if (_settings.CoreOffsetEnabled || _settings.MemoryOffsetEnabled)
                {
                    TuneRequest tr = new TuneRequest();
                    tr.SetCore = _settings.CoreOffsetEnabled;
                    tr.CoreMHz = _settings.CoreOffsetMHz;
                    tr.SetMemory = _settings.MemoryOffsetEnabled;
                    tr.MemoryMHz = _settings.MemoryOffsetMHz;
                    bool allow = _compat.Profile != null && _compat.Profile.AllowMsvdd;
                    TunerState postState;
                    OperationResult ocRes = NvApiTuner.Apply(tr, allow, out postState);
                    if (postState != null) _tuner = postState;

                    if (!String.IsNullOrEmpty(statusMsg)) statusMsg += " · ";
                    statusMsg += ocRes.Success
                        ? ("OC Core " + (_settings.CoreOffsetEnabled ? (_settings.CoreOffsetMHz >= 0 ? "+" : "") + _settings.CoreOffsetMHz.ToString() + " MHz" : "stock")
                           + ", Mem " + (_settings.MemoryOffsetEnabled ? (_settings.MemoryOffsetMHz >= 0 ? "+" : "") + _settings.MemoryOffsetMHz.ToString() + " MHz" : "stock"))
                        : ("OC FAIL: " + ocRes.Message);
                }

                if (_trayIcon != null && !String.IsNullOrEmpty(statusMsg))
                {
                    try { _trayIcon.ShowBalloonTip(2000, "NvpwrControl", "Reapplied: " + statusMsg, ToolTipIcon.Info); } catch { }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("ReapplyAll error: " + ex.Message);
            }
        }

        private int U(int x) { return (int)Math.Round(x * _userScale); }

        private void BuildUi()
        {
            SuspendLayout();

            // Use a fixed four-row shell instead of overlapping DockStyle.Top/Fill
            // controls.  This keeps page content below the navigation/header at
            // every DPI scale and fixes the clipped first row seen in v2.2.0.
            TableLayoutPanel shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = BackColor
            };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, U(52)));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, U(112)));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, U(28)));
            Controls.Add(shell);

            _nav = new Panel { Dock = DockStyle.Fill, Padding = new Padding(U(14), U(7), U(14), U(7)), Margin = new Padding(0) };
            shell.Controls.Add(_nav, 0, 0);
            FlowLayoutPanel navLeft = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            _navPower = Nav("power"); _navTune = Nav("tuning"); _navTelemetry = Nav("telemetry"); _navCompat = Nav("compatibility"); _navSettings = Nav("settings");
            navLeft.Controls.AddRange(new Control[] { _navPower, _navTune, _navTelemetry, _navCompat, _navSettings });
            _nav.Controls.Add(navLeft);
            _resetAll = ButtonBase("resetAll", U(142)); _resetAll.Dock = DockStyle.Right; _resetAll.Click += delegate { FactoryResetAsync(); }; _nav.Controls.Add(_resetAll);

            _header = new Panel { Dock = DockStyle.Fill, Padding = new Padding(U(18), U(11), U(16), U(10)), Margin = new Padding(0) };
            shell.Controls.Add(_header, 0, 1);

            _gpuName = new Label { AutoSize = false, AutoEllipsis = true, Font = new Font("Segoe UI Semibold", (float)(14.5 * _userScale)), Location = new Point(U(18), U(13)), Size = new Size(U(520), U(30)), Text = "NVIDIA RTX Laptop GPU" };
            _gpuSub = new Label { AutoSize = false, AutoEllipsis = true, Location = new Point(U(19), U(46)), Size = new Size(U(520), U(24)), Text = "Driver —  |  VBIOS —" };
            _compatBadge = new Label { AutoSize = true, Location = new Point(U(19), U(72)), Padding = new Padding(U(7), U(2), U(7), U(2)), Font = new Font("Segoe UI Semibold", (float)(8.5 * _userScale)) };
            _header.Controls.AddRange(new Control[] { _gpuName, _gpuSub, _compatBadge });

            FlowLayoutPanel metrics = new FlowLayoutPanel { Dock = DockStyle.Right, Width = U(600), FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(U(4), U(10), 0, 0), Margin = new Padding(0) };
            _headCore = HeaderMetric(metrics, "CORE");
            _headVolt = HeaderMetric(metrics, "VOLTAGE");
            _headPower = HeaderMetric(metrics, "POWER");
            _headTemp = HeaderMetric(metrics, "TEMP");
            _headHotspot = HeaderMetric(metrics, "HOTSPOT");
            _header.Controls.Add(metrics);
            _header.Resize += delegate
            {
                int leftWidth = Math.Max(U(280), metrics.Left - U(34));
                _gpuName.Width = leftWidth;
                _gpuSub.Width = leftWidth;
            };

            _content = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), Padding = new Padding(0) };
            shell.Controls.Add(_content, 0, 2);

            _status = new StatusStrip { Dock = DockStyle.Fill, AutoSize = false, SizingGrip = false, Margin = new Padding(0) };
            _statusText = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _status.Items.Add(_statusText);
            shell.Controls.Add(_status, 0, 3);

            _pagePower = Page(); _pageTune = Page(); _pageTelemetry = Page(); _pageCompat = Page(); _pageSettings = Page();
            _content.Controls.AddRange(new Control[] { _pagePower, _pageTune, _pageTelemetry, _pageCompat, _pageSettings });

            BuildPowerPage();
            BuildTunePage();
            BuildTelemetryPage();
            BuildCompatibilityPage();
            BuildSettingsPage();

            _navPower.Click += delegate { ShowPage("power"); };
            _navTune.Click += delegate { ShowPage("tuning"); };
            _navTelemetry.Click += delegate { ShowPage("telemetry"); };
            _navCompat.Click += delegate { ShowPage("compatibility"); };
            _navSettings.Click += delegate { ShowPage("settings"); };

            ResumeLayout(true);
        }

        private Label HeaderMetric(FlowLayoutPanel parent, string caption)
        {
            Panel p = new Panel { Width = U(108), Height = U(65), Margin = new Padding(U(4), 0, U(4), 0) };
            Label c = new Label { Text = caption, AutoSize = true, Location = new Point(U(5), U(7)), Font = new Font("Segoe UI", (float)(7.6 * _userScale)) };
            Label v = new Label { Text = "—", AutoSize = true, Location = new Point(U(5), U(27)), Font = new Font("Segoe UI Semibold", (float)(10.8 * _userScale)) };
            p.Controls.Add(c); p.Controls.Add(v); parent.Controls.Add(p); return v;
        }

        private NavButton Nav(string tag)
        {
            NavButton b = new NavButton { Tag = tag, Width = U(132), Margin = new Padding(U(3), 0, U(3), 0) };
            return b;
        }

        private Panel Page()
        {
            return new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(U(18), U(16), U(18), U(20)), Visible = false };
        }

        private CardPanel Card(int height, bool accent)
        {
            return new CardPanel { Height = U(height), Dock = DockStyle.Fill, AccentLine = accent };
        }

        private Label CardTitle(CardPanel p, string key)
        {
            Label l = new Label { Name = key, AutoSize = true, Font = new Font("Segoe UI Semibold", (float)(11.2 * _userScale)), Location = new Point(U(14), U(13)) };
            p.Controls.Add(l); return l;
        }

        private Button ButtonBase(string name, int width)
        {
            ThemedButton b = new ThemedButton { Name = name, Width = width, Height = U(36) };
            b.Primary = name == "applyMax" || name == "applyCurrent" || name == "applyTune" ||
                        name == "saveAutostart" || name == "validateDriver" || name == "autoVbios" ||
                        name == "rebootNow" || name == "applyVolt";
            return b;
        }

        private ComboBox ComboBase(int width)
        {
            return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = U(width), Height = U(28) };
        }

        private void BuildPowerPage()
        {
            FlowLayoutPanel root = VerticalRoot(_pagePower);
            root.Controls.Add(SectionHeader("powerSection", "powerSectionSub"));

            _rebootBanner = new Panel { Height = U(76), Padding = new Padding(U(14)), Visible = false, Margin = new Padding(U(7), 0, U(7), U(12)) };
            _rebootText = new Label { AutoSize = false, Location = new Point(U(14), U(13)), Size = new Size(U(820), U(46)) };
            _rebootNow = ButtonBase("rebootNow", U(180)); _rebootNow.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _rebootNow.Location = new Point(U(930), U(19));
            _rebootNow.Click += delegate { RebootNow(); };
            _rebootBanner.Controls.AddRange(new Control[] { _rebootText, _rebootNow });
            _responsiveWide.Add(_rebootBanner);
            root.Controls.Add(_rebootBanner);

            TableLayoutPanel grid = Grid2(U(1220));
            root.Controls.Add(grid);

            CardPanel maxCard = Card(430, true); CardTitle(maxCard, "maxTitle");
            Label maxLive = new Label { Name = "maxLiveLabel", AutoSize = true, Location = new Point(U(16), U(50)) };
            _powerMax = new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", (float)(21 * _userScale)), Location = new Point(U(16), U(70)) };
            Label maxTargetLbl = new Label { Name = "targetPower", AutoSize = true, Location = new Point(U(16), U(120)) };
            _maxTargetValue = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI Semibold", (float)(14 * _userScale)), Location = new Point(U(392), U(111)), Size = new Size(U(148), U(30)), Anchor = AnchorStyles.Top | AnchorStyles.Right, Text = "—" };
            _maxSlider = new PowerSlider { Location = new Point(U(14), U(145)), Width = U(520), Height = U(74), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            _applyMax = ButtonBase("applyMax", U(180)); _applyMax.Location = new Point(U(16), U(232));
            _restoreMax = ButtonBase("restoreMax", U(190)); _restoreMax.Location = new Point(U(210), U(232));
            _powerOverride = new Label { AutoSize = false, Location = new Point(U(16), U(280)), Size = new Size(U(515), U(130)), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            maxCard.Controls.AddRange(new Control[] { maxLive, _powerMax, maxTargetLbl, _maxTargetValue, _maxSlider, _applyMax, _restoreMax, _powerOverride });
            WirePowerCardLayout(maxCard, _maxSlider, _maxTargetValue, _powerOverride);
            grid.Controls.Add(maxCard, 0, 0);

            CardPanel curCard = Card(430, true); CardTitle(curCard, "currentTitle");
            Label curLive = new Label { Name = "currentLiveLabel", AutoSize = true, Location = new Point(U(16), U(50)) };
            _powerCurrent = new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", (float)(21 * _userScale)), Location = new Point(U(16), U(70)) };
            Label curTargetLbl = new Label { Name = "targetPower", AutoSize = true, Location = new Point(U(16), U(120)) };
            _currentTargetValue = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI Semibold", (float)(14 * _userScale)), Location = new Point(U(392), U(111)), Size = new Size(U(148), U(30)), Anchor = AnchorStyles.Top | AnchorStyles.Right, Text = "—" };
            _currentSlider = new PowerSlider { Location = new Point(U(14), U(145)), Width = U(520), Height = U(74), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            _applyCurrent = ButtonBase("applyCurrent", U(180)); _applyCurrent.Location = new Point(U(16), U(232));
            _restoreCurrent = ButtonBase("restoreCurrent", U(190)); _restoreCurrent.Location = new Point(U(210), U(232));
            _msiAutoApply = new CheckBox { Name = "msiAutoApply", AutoSize = true, Location = new Point(U(16), U(275)), Font = new Font("Segoe UI", (float)(9 * _userScale)) };
            _msiTimeoutLabel = new Label { Name = "msiTimeoutLabel", AutoSize = true, Location = new Point(U(16), U(305)) };
            _msiTimeoutValue = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI Semibold", (float)(11 * _userScale)), Location = new Point(U(392), U(301)), Size = new Size(U(148), U(24)), Anchor = AnchorStyles.Top | AnchorStyles.Right, Text = _settings.MsiTimeoutSec.ToString() + " s" };
            _msiTimeoutSlider = new PowerSlider { Location = new Point(U(14), U(328)), Width = U(520), Height = U(66), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, UnitSuffix = "s" };
            _msiTimeoutSlider.SetRange(1, 10, 1);
            _msiTimeoutSlider.Value = _settings.MsiTimeoutSec;
            _powerExperimental = new Label { AutoSize = false, Location = new Point(U(16), U(398)), Size = new Size(U(515), U(24)), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            curCard.Controls.AddRange(new Control[] { curLive, _powerCurrent, curTargetLbl, _currentTargetValue, _currentSlider, _applyCurrent, _restoreCurrent, _msiAutoApply, _msiTimeoutLabel, _msiTimeoutValue, _msiTimeoutSlider, _powerExperimental });
            WirePowerCardLayout(curCard, _currentSlider, _currentTargetValue, _powerExperimental, _msiTimeoutSlider, _msiTimeoutValue);
            grid.Controls.Add(curCard, 1, 0);

            if (MsiScenarioWatcher.IsMsiCenterInstalled())
            {
                _msiAutoApply.Checked = _settings.MsiAutoApply;
                _msiTimeoutLabel.Enabled = _settings.MsiAutoApply;
                _msiTimeoutValue.Enabled = _settings.MsiAutoApply;
                _msiTimeoutSlider.Enabled = _settings.MsiAutoApply;
                _msiAutoApply.CheckedChanged += delegate
                {
                    _settings.MsiAutoApply = _msiAutoApply.Checked;
                    _msiTimeoutLabel.Enabled = _msiAutoApply.Checked;
                    _msiTimeoutValue.Enabled = _msiAutoApply.Checked;
                    _msiTimeoutSlider.Enabled = _msiAutoApply.Checked;
                    if (_settings.MsiAutoApply) _msiWatcher.Start();
                    else _msiWatcher.Stop();
                    SaveSettings();
                };
                _msiTimeoutSlider.ValueChanged += delegate
                {
                    _settings.MsiTimeoutSec = _msiTimeoutSlider.Value;
                    if (_msiWatcher != null) _msiWatcher.TimeoutSec = _settings.MsiTimeoutSec;
                    if (_msiTimeoutValue != null) _msiTimeoutValue.Text = _settings.MsiTimeoutSec.ToString() + " s";
                    if (_msiTimeoutSliderSettings != null && _msiTimeoutSliderSettings.Value != _msiTimeoutSlider.Value)
                        _msiTimeoutSliderSettings.Value = _msiTimeoutSlider.Value;
                    SaveSettings();
                };
            }
            else
            {
                _msiAutoApply.Visible = false;
                _msiTimeoutLabel.Visible = false;
                _msiTimeoutValue.Visible = false;
                _msiTimeoutSlider.Visible = false;
            }

            CardPanel voltCard = Card(324, true); CardTitle(voltCard, "voltTitle");
            Label voltLive = new Label { Name = "voltLiveLabel", AutoSize = true, Location = new Point(U(16), U(50)) };
            _powerVoltage = new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", (float)(21 * _userScale)), Location = new Point(U(16), U(70)), Text = "—" };
            Label voltTargetLbl = new Label { Name = "targetVoltage", AutoSize = true, Location = new Point(U(16), U(120)) };
            _voltTargetValue = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI Semibold", (float)(14 * _userScale)), Location = new Point(U(392), U(111)), Size = new Size(U(148), U(30)), Anchor = AnchorStyles.Top | AnchorStyles.Right, Text = "—" };
            _voltSlider = new PowerSlider { Location = new Point(U(14), U(145)), Width = U(520), Height = U(74), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, UnitSuffix = "mV" };
            _applyVolt = ButtonBase("applyVolt", U(150)); _applyVolt.Location = new Point(U(16), U(232));
            _restartDriverVolt = ButtonBase("restartDriver", U(170)); _restartDriverVolt.Location = new Point(U(176), U(232));
            _restoreVolt = ButtonBase("restoreVolt", U(160)); _restoreVolt.Location = new Point(U(356), U(232));
            _voltStatus = new Label { AutoSize = false, Location = new Point(U(16), U(280)), Size = new Size(U(515), U(32)), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            voltCard.Controls.AddRange(new Control[] { voltLive, _powerVoltage, voltTargetLbl, _voltTargetValue, _voltSlider, _applyVolt, _restartDriverVolt, _restoreVolt, _voltStatus });
            WirePowerCardLayout(voltCard, _voltSlider, _voltTargetValue, _voltStatus);
            grid.Controls.Add(voltCard, 0, 1);

            CardPanel voltInfoCard = Card(324, false); CardTitle(voltInfoCard, "voltInfoTitle");
            Label voltInfoDesc = new Label { Name = "voltInfoDesc", AutoSize = false, Location = new Point(U(16), U(50)), Size = new Size(U(510), U(255)), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom };
            voltInfoCard.Controls.Add(voltInfoDesc);
            grid.Controls.Add(voltInfoCard, 1, 1);

            CardPanel startCard = Card(190, false); CardTitle(startCard, "startupTitle");
            Label startDesc = new Label { Name = "startupDesc", AutoSize = false, Location = new Point(U(16), U(50)), Size = new Size(U(510), U(52)) };
            _autostart = ButtonBase("saveAutostart", U(190)); _autostart.Location = new Point(U(16), U(118));
            _removeAutostart = ButtonBase("removeAutostart", U(190)); _removeAutostart.Location = new Point(U(220), U(118));
            startCard.Controls.AddRange(new Control[] { startDesc, _autostart, _removeAutostart });
            grid.Controls.Add(startCard, 0, 2);

            CardPanel flowCard = Card(190, false); CardTitle(flowCard, "workflowTitle");
            _workflowText = new Label { AutoSize = false, Location = new Point(U(16), U(50)), Size = new Size(U(510), U(122)) };
            flowCard.Controls.Add(_workflowText); grid.Controls.Add(flowCard, 1, 2);

            _powerRange = new Label { AutoSize = true, Margin = new Padding(U(10), U(10), 0, U(8)) };
            root.Controls.Add(_powerRange);

            _applyMax.Click += delegate { ApplyMaxAsync(); };
            _applyCurrent.Click += delegate { ApplyCurrentAsync(); };
            _restoreCurrent.Click += delegate { RestoreCurrentAsync(); };
            _restoreMax.Click += delegate { RestoreMaxAsync(); };
            _applyVolt.Click += delegate { ApplyVoltageAsync(); };
            _restartDriverVolt.Click += delegate { RestartNvidiaAsync(); };
            _restoreVolt.Click += delegate { RestoreVoltageAsync(); };
            _autostart.Click += delegate { SaveAutostartAsync(); };
            _removeAutostart.Click += delegate { RunOperationAsync(delegate { return _power.RemoveAutostart(); }, true); };
            _maxSlider.ValueChanged += delegate { _settings.MaxSelection = _maxSlider.Value; UpdatePowerTargetLabels(); SaveSettings(); };
            _currentSlider.ValueChanged += delegate { _settings.CurrentSelection = _currentSlider.Value; UpdatePowerTargetLabels(); UpdateExperimentalLabel(); SaveSettings(); };
            _voltSlider.ValueChanged += delegate { _settings.VoltageSelection = _voltSlider.Value; UpdateVoltageTargetLabels(); SaveSettings(); };
        }

        private void BuildTunePage()
        {
            FlowLayoutPanel root = VerticalRoot(_pageTune);
            root.Controls.Add(SectionHeader("tuningSection", "tuningSectionSub"));
            TableLayoutPanel grid = Grid2(U(1220)); root.Controls.Add(grid);

            int row = 0;
            CardPanel c1 = TuneCard("coreTitle", "coreTarget", out _coreEnable, out _core, out _coreRange, -1000, 1000, 1, "MHz"); grid.Controls.Add(c1, 0, row);
            CardPanel c2 = TuneCard("memoryTitle", "memoryTarget", out _memEnable, out _mem, out _memRange, -1000, 5000, 1, "MHz"); grid.Controls.Add(c2, 1, row++);
            CardPanel c3 = TuneCard("xbarTitle", "xbarTarget", out _xbarEnable, out _xbar, out _xbarRange, -1000, 1000, 1, "MHz"); grid.Controls.Add(c3, 0, row);
            CardPanel c4 = TuneCard("msvddTitle", "msvddTarget", out _msvddEnable, out _msvdd, out _msvddRange, -100, 100, 1, "mV"); grid.Controls.Add(c4, 1, row++);
            CardPanel c5 = TuneCard("nvvddTitle", "nvvddTarget", out _nvvddEnable, out _nvvdd, out _nvvddRange, -200, 200, 1, "mV"); grid.Controls.Add(c5, 0, row);
            CardPanel c6 = RatioCard(); grid.Controls.Add(c6, 1, row++);

            CardPanel actions = Card(150, true); actions.Width = U(1220); actions.Dock = DockStyle.None; _responsiveWide.Add(actions);
            _probeTune = ButtonBase("probeTune", U(180)); _probeTune.Location = new Point(U(14), U(22));
            _applyTune = ButtonBase("applyTune", U(180)); _applyTune.Location = new Point(U(210), U(22));
            _resetTune = ButtonBase("resetTune", U(190)); _resetTune.Location = new Point(U(406), U(22));
            _tuneSummary = new Label { AutoSize = false, Location = new Point(U(620), U(16)), Size = new Size(U(480), U(115)) };
            actions.Controls.AddRange(new Control[] { _probeTune, _applyTune, _resetTune, _tuneSummary });
            actions.Resize += delegate { _tuneSummary.Width = Math.Max(U(220), actions.ClientSize.Width - U(640)); };
            root.Controls.Add(actions);

            _probeTune.Click += delegate { ProbeTuningAsync(); };
            _applyTune.Click += delegate { ApplyTuningAsync(); };
            _resetTune.Click += delegate { ResetTuningAsync(); };
        }

        private CardPanel TuneCard(string titleKey, string targetKey, out CheckBox enable, out NumericUpDown value, out Label range, int min, int max, int inc, string unit)
        {
            CardPanel p = Card(190, false); CardTitle(p, titleKey);
            CheckBox cb = new CheckBox { Name = "enabledToggle", AutoSize = true, Text = "Enabled" };
            enable = cb;
            Label target = new Label { Name = targetKey, AutoSize = true, Location = new Point(U(14), U(60)) };
            NumericUpDown num = new NumericUpDown { Minimum = min, Maximum = max, Increment = inc, Width = U(135), Location = new Point(U(14), U(86)), ThousandsSeparator = false };
            value = num;
            Label u = new Label { Text = unit, AutoSize = true, Location = new Point(U(158), U(90)) };
            Label lblRange = new Label { AutoSize = false, Location = new Point(U(14), U(132)), Size = new Size(U(500), U(42)) };
            range = lblRange;
            p.Controls.AddRange(new Control[] { cb, target, num, u, lblRange });

            Action repositionCb = delegate {
                cb.Location = new Point(Math.Max(U(14), p.ClientSize.Width - cb.PreferredSize.Width - U(18)), U(14));
            };
            p.Resize += delegate { repositionCb(); lblRange.Width = Math.Max(U(200), p.ClientSize.Width - U(28)); };
            cb.SizeChanged += delegate { repositionCb(); };
            repositionCb();

            cb.CheckedChanged += delegate { UpdateWriteButtons(); };
            num.ValueChanged += delegate {
                if (!cb.Checked && num.Value != 0) cb.Checked = true;
                UpdateWriteButtons();
            };

            return p;
        }

        private CardPanel RatioCard()
        {
            CardPanel p = Card(190, false); CardTitle(p, "ratioTitle");
            _ratioEnable = new CheckBox { Name = "enabledToggle", AutoSize = true, Text = "Enabled" };
            Label target = new Label { Name = "ratioTarget", AutoSize = true, Location = new Point(U(14), U(60)) };
            _ratio = new NumericUpDown { DecimalPlaces = 4, Increment = 0.01m, Minimum = 0m, Maximum = 2m, Width = U(135), Location = new Point(U(14), U(86)) };
            _ratioRange = new Label { AutoSize = false, Location = new Point(U(14), U(132)), Size = new Size(U(500), U(42)) };
            p.Controls.AddRange(new Control[] { _ratioEnable, target, _ratio, _ratioRange });

            Action repositionCb = delegate {
                _ratioEnable.Location = new Point(Math.Max(U(14), p.ClientSize.Width - _ratioEnable.PreferredSize.Width - U(18)), U(14));
            };
            p.Resize += delegate { repositionCb(); _ratioRange.Width = Math.Max(U(200), p.ClientSize.Width - U(28)); };
            _ratioEnable.SizeChanged += delegate { repositionCb(); };
            repositionCb();

            _ratioEnable.CheckedChanged += delegate { UpdateWriteButtons(); };
            _ratio.ValueChanged += delegate {
                if (!_ratioEnable.Checked && _ratio.Value != 0m) _ratioEnable.Checked = true;
                UpdateWriteButtons();
            };

            return p;
        }

        private void BuildTelemetryPage()
        {
            FlowLayoutPanel root = VerticalRoot(_pageTelemetry);
            root.Controls.Add(SectionHeader("telemetrySection", "telemetrySectionSub"));

            CardPanel toolbar = Card(54, false);
            toolbar.Dock = DockStyle.None;
            toolbar.Width = U(1220);
            _responsiveWide.Add(toolbar);

            _telemetryTogglePage = new CheckBox
            {
                Name = "telemetryPageToggle",
                AutoSize = true,
                Location = new Point(U(16), U(14)),
                Checked = _settings.TelemetryEnabled,
                Font = new Font("Segoe UI Semibold", (float)(9.5 * _userScale))
            };

            _telemetryRefreshBtn = ButtonBase("telemetryManualRefresh", U(170));
            _telemetryRefreshBtn.Location = new Point(U(360), U(10));
            _telemetryRefreshBtn.Height = U(32);

            _telemetryStatusLabel = new Label
            {
                AutoSize = true,
                Location = new Point(U(550), U(16)),
                Font = new Font("Segoe UI", (float)(9.0 * _userScale)),
                ForeColor = _settings.TelemetryEnabled ? _ok : _muted
            };
            if (!_settings.TelemetryEnabled)
                _telemetryStatusLabel.Text = T("telemetryStatusOff");
            else
                _telemetryStatusLabel.Text = _settings.Language == "ru"
                    ? ("Автообновление активно (" + _settings.TelemetryIntervalMs + " мс)")
                    : ("Live polling active (" + _settings.TelemetryIntervalMs + " ms)");

            toolbar.Controls.AddRange(new Control[] { _telemetryTogglePage, _telemetryRefreshBtn, _telemetryStatusLabel });
            root.Controls.Add(toolbar);

            _telemetryGrid = new TableLayoutPanel { Width = U(1220), Height = U(360), ColumnCount = 3, RowCount = 3, Margin = new Padding(0) };
            _telemetryGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f)); _telemetryGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f)); _telemetryGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            for (int i = 0; i < 3; i++) _telemetryGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, U(118)));
            _monPower = MetricCard(_telemetryGrid, 0, 0, "metricPower"); _monTemp = MetricCard(_telemetryGrid, 1, 0, "metricTemp"); _monHotspot = MetricCard(_telemetryGrid, 2, 0, "metricHotspot");
            _monMemTemp = MetricCard(_telemetryGrid, 0, 1, "metricMemTemp"); _monUtil = MetricCard(_telemetryGrid, 1, 1, "metricUtil"); _monCore = MetricCard(_telemetryGrid, 2, 1, "metricCore");
            _monMem = MetricCard(_telemetryGrid, 0, 2, "metricMem"); _monCurrent = MetricCard(_telemetryGrid, 1, 2, "metricCurrent"); _monMax = MetricCard(_telemetryGrid, 2, 2, "metricMax");
            _responsiveWide.Add(_telemetryGrid);
            root.Controls.Add(_telemetryGrid);
            _restartNv = ButtonBase("restartNv", U(220)); _restartNv.Margin = new Padding(U(7), U(12), 0, 0); root.Controls.Add(_restartNv);
            _restartNv.Click += delegate { RestartNvidiaAsync(); };

            _telemetryTogglePage.CheckedChanged += delegate
            {
                UpdateTelemetryState(_telemetryTogglePage.Checked);
            };
            _telemetryRefreshBtn.Click += delegate
            {
                ForceRefreshTelemetryAsync();
            };
            if (!_settings.TelemetryEnabled)
            {
                ClearTelemetryUi();
            }
        }

        private void UpdateTelemetryState(bool enabled)
        {
            _settings.TelemetryEnabled = enabled;
            if (_telemetryEnable != null && _telemetryEnable.Checked != enabled) _telemetryEnable.Checked = enabled;
            if (_telemetryTogglePage != null && _telemetryTogglePage.Checked != enabled) _telemetryTogglePage.Checked = enabled;

            if (enabled)
            {
                if (Visible && WindowState != FormWindowState.Minimized)
                {
                    if (_timer != null)
                    {
                        _timer.Interval = Math.Max(500, _settings.TelemetryIntervalMs);
                        _timer.Start();
                    }
                    RefreshTelemetryAsync();
                }
                if (_telemetryStatusLabel != null)
                {
                    _telemetryStatusLabel.Text = _settings.Language == "ru"
                        ? ("Автообновление активно (" + _settings.TelemetryIntervalMs + " мс)")
                        : ("Live polling active (" + _settings.TelemetryIntervalMs + " ms)");
                    _telemetryStatusLabel.ForeColor = _ok;
                }
            }
            else
            {
                if (_timer != null) _timer.Stop();
                if (_telemetryStatusLabel != null)
                {
                    _telemetryStatusLabel.Text = T("telemetryStatusOff");
                    _telemetryStatusLabel.ForeColor = _muted;
                }
                ClearTelemetryUi();
            }
            SaveSettings();
        }

        private void ClearTelemetryUi()
        {
            if (_monPower != null) _monPower.Text = "— (Off)";
            if (_monTemp != null) _monTemp.Text = "— (Off)";
            if (_monHotspot != null) _monHotspot.Text = "— (Off)";
            if (_monMemTemp != null) _monMemTemp.Text = "— (Off)";
            if (_monUtil != null) _monUtil.Text = "— (Off)";
            if (_monCore != null) _monCore.Text = "— (Off)";
            if (_monMem != null) _monMem.Text = "— (Off)";
            if (_monCurrent != null) _monCurrent.Text = "— (Off)";
            if (_monMax != null) _monMax.Text = "— (Off)";
        }

        private Label MetricCard(TableLayoutPanel grid, int col, int row, string titleKey)
        {
            CardPanel p = Card(100, false); p.Dock = DockStyle.Fill;
            Label t = new Label { Name = titleKey, AutoSize = false, AutoEllipsis = true, Location = new Point(U(14), U(16)), Size = new Size(U(320), U(24)) };
            Label v = new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", (float)(17 * _userScale)), Location = new Point(U(14), U(46)), Text = "—" };
            p.Controls.AddRange(new Control[] { t, v });
            p.Resize += delegate { t.Width = Math.Max(U(120), p.ClientSize.Width - U(28)); };
            grid.Controls.Add(p, col, row); return v;
        }

        private void BuildCompatibilityPage()
        {
            FlowLayoutPanel root = VerticalRoot(_pageCompat);
            root.Controls.Add(SectionHeader("compatSection", "compatSectionSub"));

            TableLayoutPanel statusGrid = new TableLayoutPanel
            {
                Height = U(118), ColumnCount = 4, RowCount = 1, Margin = new Padding(0), Padding = new Padding(0)
            };
            statusGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            statusGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            statusGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            statusGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            _compatGpuState = ResolverStatusCard(statusGrid, 0, "resolverGpu");
            _compatDriverState = ResolverStatusCard(statusGrid, 1, "resolverDriver");
            _compatVbiosState = ResolverStatusCard(statusGrid, 2, "resolverVbios");
            _compatPolicyState = ResolverStatusCard(statusGrid, 3, "resolverPolicy");
            _responsiveWide.Add(statusGrid);
            root.Controls.Add(statusGrid);

            CardPanel buttons = Card(166, true); buttons.Dock = DockStyle.None;
            CardTitle(buttons, "compatActionsTitle");
            FlowLayoutPanel actions = new FlowLayoutPanel { Location = new Point(U(14), U(57)), Height = U(94), AutoSize = false, WrapContents = true, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            _validateDriver = ButtonBase("validateDriver", U(200));
            _autoVbios = ButtonBase("autoVbios", U(190));
            _selectRom = ButtonBase("selectRom", U(155));
            _recheck = ButtonBase("recheck", U(130));
            _exportReport = ButtonBase("exportReport", U(145));
            _openLog = ButtonBase("openLog", U(125));
            foreach (Control b in new Control[] { _validateDriver, _autoVbios, _selectRom, _recheck, _exportReport, _openLog }) b.Margin = new Padding(0, 0, U(10), 0);
            actions.Controls.AddRange(new Control[] { _validateDriver, _autoVbios, _selectRom, _recheck, _exportReport, _openLog });
            buttons.Controls.Add(actions);
            _responsiveWide.Add(buttons);
            root.Controls.Add(buttons);

            CardPanel report = Card(520, false); report.Dock = DockStyle.None;
            CardTitle(report, "compatReportTitle");
            _compatText = new TextBox { Multiline = true, ReadOnly = true, WordWrap = false, ScrollBars = ScrollBars.Both, Location = new Point(U(14), U(48)), Font = new Font("Consolas", (float)(8.8 * _userScale)), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            report.Controls.Add(_compatText);
            _responsiveWide.Add(report);
            root.Controls.Add(report);

            report.Resize += delegate { _compatText.Size = new Size(Math.Max(U(200), report.ClientSize.Width - U(28)), Math.Max(U(180), report.ClientSize.Height - U(64))); };
            buttons.Resize += delegate { actions.Width = Math.Max(U(200), buttons.ClientSize.Width - U(28)); };

            _validateDriver.Click += delegate { ValidateDriverAsync(); };
            _autoVbios.Click += delegate { ResolveVbiosAutoAsync(); };
            _selectRom.Click += delegate { SelectRomAndResolveAsync(); };
            _recheck.Click += delegate { RefreshEverythingAsync(); };
            _exportReport.Click += delegate { ExportReport(); };
            _openLog.Click += delegate { try { if (File.Exists(AppLog.LogPath)) System.Diagnostics.Process.Start("notepad.exe", "\"" + AppLog.LogPath + "\""); } catch { } };
        }

        private Label ResolverStatusCard(TableLayoutPanel grid, int col, string titleKey)
        {
            CardPanel p = Card(104, false); p.Dock = DockStyle.Fill;
            Label t = new Label { Name = titleKey, AutoSize = false, AutoEllipsis = true, Location = new Point(U(14), U(14)), Size = new Size(U(230), U(22)), Font = new Font("Segoe UI Semibold", (float)(9.8 * _userScale)) };
            Label v = new Label { AutoSize = false, AutoEllipsis = true, Location = new Point(U(14), U(45)), Size = new Size(U(245), U(46)), Font = new Font("Segoe UI", (float)(9.0 * _userScale)) };
            p.Controls.AddRange(new Control[] { t, v });
            p.Resize += delegate
            {
                int w = Math.Max(U(100), p.ClientSize.Width - U(28));
                t.Width = w;
                v.Width = w;
            };
            grid.Controls.Add(p, col, 0);
            return v;
        }

        private void BuildSettingsPage()
        {
            FlowLayoutPanel root = VerticalRoot(_pageSettings);
            root.Controls.Add(SectionHeader("settingsSection", "settingsSectionSub"));
            TableLayoutPanel grid = Grid2(U(1220)); root.Controls.Add(grid);

            CardPanel appearance = Card(430, true); CardTitle(appearance, "appearanceTitle");
            Label l1 = SettingLabel(appearance, "language", 58); _lang = ComboBase(190); _lang.Items.AddRange(new object[] { "Русский", "English" }); _lang.Location = new Point(U(250), U(52));
            Label l2 = SettingLabel(appearance, "theme", 106); _theme = ComboBase(190); _theme.Items.AddRange(new object[] { "Graphite", "Midnight", "Light" }); _theme.Location = new Point(U(250), U(100));
            Label l3 = SettingLabel(appearance, "accent", 154); _accent = ColorCombo(); _accent.Location = new Point(U(250), U(148));
            Label l4 = SettingLabel(appearance, "buttonAccent", 202); _buttonAccent = ColorCombo(); _buttonAccent.Location = new Point(U(250), U(196));
            Label l5 = SettingLabel(appearance, "sliderAccent", 250); _sliderAccent = ColorCombo(); _sliderAccent.Location = new Point(U(250), U(244));
            Label l6 = SettingLabel(appearance, "uiScale", 298); _uiScale = ComboBase(190); _uiScale.Items.AddRange(new object[] { "80%", "90%", "100%", "110%", "125%", "140%", "150%" }); _uiScale.Location = new Point(U(250), U(292));
            appearance.Controls.AddRange(new Control[] { l1, _lang, l2, _theme, l3, _accent, l4, _buttonAccent, l5, _sliderAccent, l6, _uiScale });
            grid.Controls.Add(appearance, 0, 0);

            CardPanel behavior = Card(430, false); CardTitle(behavior, "behaviorTitle");
            _telemetryEnable = new CheckBox { Name = "telemetryEnabled", Location = new Point(U(16), U(52)), AutoSize = true };
            _closeToTray = new CheckBox { Name = "closeToTray", Location = new Point(U(16), U(82)), AutoSize = true, Checked = _settings.CloseToTray };
            Label ti = new Label { Name = "telemetryInterval", AutoSize = true, Location = new Point(U(16), U(118)) };
            _telemetryMs = new NumericUpDown { Minimum = 500, Maximum = 10000, Increment = 250, Width = U(155), Location = new Point(U(250), U(112)) };
            Label ms = new Label { Text = "ms", AutoSize = true, Location = new Point(U(415), U(116)) };
            Label msiSetLbl = new Label { Name = "msiTimeoutLabel", AutoSize = true, Location = new Point(U(16), U(160)) };
            _msiTimeoutSliderSettings = new PowerSlider { Location = new Point(U(14), U(185)), Width = U(520), Height = U(66), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, UnitSuffix = "s" };
            _msiTimeoutSliderSettings.SetRange(1, 10, 1);
            _msiTimeoutSliderSettings.Value = _settings.MsiTimeoutSec;
            _settingsNote = new Label { AutoSize = false, Location = new Point(U(16), U(260)), Size = new Size(U(510), U(150)) };
            behavior.Controls.AddRange(new Control[] { _telemetryEnable, _closeToTray, ti, _telemetryMs, ms, msiSetLbl, _msiTimeoutSliderSettings, _settingsNote });
            behavior.Resize += delegate { _msiTimeoutSliderSettings.Width = Math.Max(U(260), behavior.ClientSize.Width - U(30)); };
            grid.Controls.Add(behavior, 1, 0);

            _lang.SelectedIndexChanged += delegate { if (_lang.SelectedIndex >= 0) { _settings.Language = _lang.SelectedIndex == 1 ? "en" : "ru"; ApplyLanguage(); SaveSettings(); } };
            _theme.SelectedIndexChanged += delegate
            {
                if (_theme.SelectedIndex >= 0)
                {
                    _settings.Theme = _theme.SelectedIndex == 2 ? "light" : (_theme.SelectedIndex == 1 ? "midnight" : "dark");
                    ApplyTheme(); SaveSettings();
                }
            };
            _accent.SelectedIndexChanged += delegate { HandleColorSelection(_accent, "accent"); };
            _buttonAccent.SelectedIndexChanged += delegate { HandleColorSelection(_buttonAccent, "button"); };
            _sliderAccent.SelectedIndexChanged += delegate { HandleColorSelection(_sliderAccent, "slider"); };
            _telemetryEnable.CheckedChanged += delegate { UpdateTelemetryState(_telemetryEnable.Checked); };
            _closeToTray.CheckedChanged += delegate { _settings.CloseToTray = _closeToTray.Checked; SaveSettings(); };
            _telemetryMs.ValueChanged += delegate
            {
                _settings.TelemetryIntervalMs = (int)_telemetryMs.Value;
                if (_timer != null) _timer.Interval = _settings.TelemetryIntervalMs;
                if (_telemetryStatusLabel != null && _settings.TelemetryEnabled)
                {
                    _telemetryStatusLabel.Text = _settings.Language == "ru"
                        ? ("Автообновление активно (" + _settings.TelemetryIntervalMs + " мс)")
                        : ("Live polling active (" + _settings.TelemetryIntervalMs + " ms)");
                }
                SaveSettings();
            };
            _msiTimeoutSliderSettings.ValueChanged += delegate
            {
                if (_msiTimeoutSlider != null && _msiTimeoutSlider.Value != _msiTimeoutSliderSettings.Value)
                    _msiTimeoutSlider.Value = _msiTimeoutSliderSettings.Value;
                _settings.MsiTimeoutSec = _msiTimeoutSliderSettings.Value;
                if (_msiWatcher != null) _msiWatcher.TimeoutSec = _settings.MsiTimeoutSec;
                if (_msiTimeoutValue != null) _msiTimeoutValue.Text = _settings.MsiTimeoutSec.ToString() + " s";
                SaveSettings();
            };
        }

        private ComboBox ColorCombo()
        {
            ComboBox c = ComboBase(190);
            c.Items.AddRange(new object[] { "Purple", "Blue", "Cyan", "Green", "Orange", "Red", "Custom…" });
            return c;
        }

        private void HandleColorSelection(ComboBox combo, string target)
        {
            if (combo == null || combo.SelectedIndex < 0) return;
            string selected = Convert.ToString(combo.SelectedItem) ?? "";
            string value = selected;
            if (selected == "Custom…")
            {
                string existing = target == "button" ? _settings.ButtonAccent : (target == "slider" ? _settings.SliderAccent : _settings.Accent);
                using (ColorDialog dlg = new ColorDialog())
                {
                    dlg.FullOpen = true;
                    dlg.Color = AccentColor(existing);
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                    {
                        SelectColorCombo(combo, existing);
                        return;
                    }
                    value = "#" + dlg.Color.R.ToString("X2") + dlg.Color.G.ToString("X2") + dlg.Color.B.ToString("X2");
                }
                int customIndex = combo.Items.Count - 1;
                combo.Items.Insert(customIndex, value);
                combo.SelectedItem = value;
            }
            else if (!selected.StartsWith("#", StringComparison.Ordinal))
            {
                value = selected.ToLowerInvariant();
            }

            if (target == "button") _settings.ButtonAccent = value;
            else if (target == "slider") _settings.SliderAccent = value;
            else _settings.Accent = value;
            ApplyTheme();
            SaveSettings();
        }

        private Label SettingLabel(Control p, string name, int y)
        {
            Label l = new Label { Name = name, AutoSize = true, Location = new Point(U(14), U(y)) }; p.Controls.Add(l); return l;
        }

        private FlowLayoutPanel VerticalRoot(Panel page)
        {
            FlowLayoutPanel root = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Width = U(1220),
                Padding = new Padding(0, U(4), 0, U(18)),
                Margin = new Padding(0),
                Location = new Point(U(12), U(4))
            };
            _responsiveRoots.Add(root);
            page.Controls.Add(root);
            return root;
        }

        private Panel SectionHeader(string titleKey, string subKey)
        {
            Panel p = new Panel { Width = U(1220), Height = U(82), Margin = new Padding(U(7), U(4), U(7), U(14)) };
            Label line = new Label { AutoSize = false, Location = new Point(0, U(9)), Size = new Size(U(3), U(50)) };
            line.Name = "accentLine";
            Label title = new Label { Name = titleKey, AutoSize = true, Font = new Font("Segoe UI Semibold", (float)(12.6 * _userScale)), Location = new Point(U(15), U(5)) };
            Label sub = new Label { Name = subKey, AutoSize = false, AutoEllipsis = true, Location = new Point(U(15), U(36)), Size = new Size(U(1160), U(38)), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            p.Controls.AddRange(new Control[] { line, title, sub });
            p.Resize += delegate { sub.Width = Math.Max(U(200), p.ClientSize.Width - U(30)); };
            _responsiveWide.Add(p);
            return p;
        }

        private TableLayoutPanel Grid2(int width)
        {
            int gap = U(14);
            int column = Math.Max(U(440), (width - gap) / 2);
            TableLayoutPanel g = new TableLayoutPanel
            {
                Width = width,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
                ColumnCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                GrowStyle = TableLayoutPanelGrowStyle.AddRows
            };
            g.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, column));
            g.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, column));
            _responsiveGrid2.Add(g);
            _responsiveWide.Add(g);
            return g;
        }

        private void LayoutResponsivePages()
        {
            foreach (FlowLayoutPanel root in _responsiveRoots)
            {
                Panel page = root.Parent as Panel;
                if (page == null) continue;

                int rawAvailable = Math.Max(U(620), page.ClientSize.Width - U(38));
                int target = Math.Min(U(1760), rawAvailable);
                root.MinimumSize = new Size(target, 0);
                root.Width = target;
                root.Left = Math.Max(U(8), (page.ClientSize.Width - target) / 2);
                root.Top = U(4);

                foreach (Control c in root.Controls)
                {
                    if (_responsiveWide.Contains(c))
                    {
                        int w = Math.Max(U(600), target - U(14));
                        c.MinimumSize = new Size(w, 0);
                        c.Width = w;
                    }
                }
            }

            foreach (TableLayoutPanel g in _responsiveGrid2)
            {
                CaptureGridPositions(g);
                FlowLayoutPanel root = g.Parent as FlowLayoutPanel;
                int available = root != null ? Math.Max(U(600), root.Width - U(14)) : Math.Max(U(600), g.Width);
                g.MinimumSize = new Size(available, 0);
                g.Width = available;

                bool singleColumn = available < U(1080);
                ApplyGridMode(g, singleColumn);

                if (!singleColumn && g.ColumnStyles.Count >= 2)
                {
                    int gap = U(14);
                    int column = Math.Max(U(430), (available - gap) / 2);
                    g.ColumnStyles[0].SizeType = SizeType.Absolute;
                    g.ColumnStyles[1].SizeType = SizeType.Absolute;
                    g.ColumnStyles[0].Width = column;
                    g.ColumnStyles[1].Width = column;
                }
                else if (singleColumn && g.ColumnStyles.Count >= 2)
                {
                    g.ColumnStyles[0].SizeType = SizeType.Absolute;
                    g.ColumnStyles[1].SizeType = SizeType.Absolute;
                    g.ColumnStyles[0].Width = Math.Max(U(560), available - U(2));
                    g.ColumnStyles[1].Width = 0;
                }
            }

            if (_telemetryGrid != null)
            {
                FlowLayoutPanel root = _telemetryGrid.Parent as FlowLayoutPanel;
                if (root != null)
                {
                    int w = Math.Max(U(600), root.Width - U(14));
                    _telemetryGrid.MinimumSize = new Size(w, 0);
                    _telemetryGrid.Width = w;
                }
            }

            if (_rebootBanner != null && _rebootNow != null)
            {
                _rebootNow.Left = Math.Max(U(420), _rebootBanner.ClientSize.Width - _rebootNow.Width - U(18));
                _rebootText.Width = Math.Max(U(300), _rebootNow.Left - U(42));
            }
        }

        private void CaptureGridPositions(TableLayoutPanel g)
        {
            if (_gridOriginalPositions.ContainsKey(g)) return;
            Dictionary<Control, TableLayoutPanelCellPosition> map = new Dictionary<Control, TableLayoutPanelCellPosition>();
            foreach (Control c in g.Controls)
                map[c] = g.GetPositionFromControl(c);
            _gridOriginalPositions[g] = map;
            _gridSingleColumn[g] = false;
        }

        private void ApplyGridMode(TableLayoutPanel g, bool singleColumn)
        {
            bool old;
            if (_gridSingleColumn.TryGetValue(g, out old) && old == singleColumn) return;

            Dictionary<Control, TableLayoutPanelCellPosition> map;
            if (!_gridOriginalPositions.TryGetValue(g, out map)) return;

            List<Control> controls = new List<Control>(map.Keys);
            controls.Sort(delegate(Control a, Control b)
            {
                TableLayoutPanelCellPosition pa = map[a];
                TableLayoutPanelCellPosition pb = map[b];
                int row = pa.Row.CompareTo(pb.Row);
                return row != 0 ? row : pa.Column.CompareTo(pb.Column);
            });

            g.SuspendLayout();
            g.Controls.Clear();
            if (singleColumn)
            {
                g.RowCount = Math.Max(1, controls.Count);
                for (int i = 0; i < controls.Count; i++)
                    g.Controls.Add(controls[i], 0, i);
            }
            else
            {
                int maxRow = 0;
                foreach (Control c in controls)
                {
                    TableLayoutPanelCellPosition pos = map[c];
                    maxRow = Math.Max(maxRow, pos.Row);
                    g.Controls.Add(c, pos.Column, pos.Row);
                }
                g.RowCount = maxRow + 1;
            }
            g.ResumeLayout(true);
            _gridSingleColumn[g] = singleColumn;
        }

        private void WirePowerCardLayout(CardPanel card, PowerSlider slider, Label targetValue, Label footer, PowerSlider secondSlider = null, Label secondValue = null)
        {
            EventHandler layout = delegate
            {
                int inner = Math.Max(U(260), card.ClientSize.Width - U(30));
                slider.Left = U(14);
                slider.Width = inner;
                targetValue.Left = Math.Max(U(220), card.ClientSize.Width - targetValue.Width - U(16));
                footer.Width = Math.Max(U(220), card.ClientSize.Width - U(32));
                if (secondSlider != null)
                {
                    secondSlider.Left = U(14);
                    secondSlider.Width = inner;
                }
                if (secondValue != null)
                {
                    secondValue.Left = Math.Max(U(220), card.ClientSize.Width - secondValue.Width - U(16));
                }
            };
            card.Resize += layout;
            layout(card, EventArgs.Empty);
        }

        private void ApplySettingsSelectors()
        {
            if (_lang != null) _lang.SelectedIndex = _settings.Language == "en" ? 1 : 0;
            if (_theme != null) _theme.SelectedIndex = _settings.Theme == "light" ? 2 : (_settings.Theme == "midnight" ? 1 : 0);
            SelectColorCombo(_accent, _settings.Accent);
            SelectColorCombo(_buttonAccent, _settings.ButtonAccent);
            SelectColorCombo(_sliderAccent, _settings.SliderAccent);
            if (_uiScale != null)
            {
                int[] xs = new int[] { 80, 90, 100, 110, 125, 140, 150 }; int idx = 2;
                for (int i = 0; i < xs.Length; i++) if (xs[i] == _settings.UiScalePercent) idx = i;
                _uiScale.SelectedIndex = idx;
            }
            if (_telemetryEnable != null) _telemetryEnable.Checked = _settings.TelemetryEnabled;
            if (_telemetryTogglePage != null) _telemetryTogglePage.Checked = _settings.TelemetryEnabled;
            if (_telemetryMs != null) _telemetryMs.Value = Math.Max(_telemetryMs.Minimum, Math.Min(_telemetryMs.Maximum, (decimal)_settings.TelemetryIntervalMs));
        }

        private void SelectColorCombo(ComboBox combo, string value)
        {
            if (combo == null) return;
            string raw = value ?? "purple";
            if (raw.StartsWith("#", StringComparison.Ordinal) && raw.Length == 7)
            {
                int existing = combo.Items.IndexOf(raw);
                if (existing < 0)
                {
                    int customIndex = Math.Max(0, combo.Items.Count - 1);
                    combo.Items.Insert(customIndex, raw);
                    existing = customIndex;
                }
                combo.SelectedIndex = existing;
                return;
            }
            string a = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(raw);
            int i = combo.Items.IndexOf(a);
            combo.SelectedIndex = i >= 0 ? i : 0;
        }

        private async void RefreshEverythingAsync()
        {
            if (_busy) return; _busy = true; SetStatus(T("checking")); UpdateWriteButtons();
            try
            {
                CompatibilityState c = await Task.Run(delegate { return _power.CheckCompatibility(); });

                // Unknown VBIOSes are now resolved automatically once per app session.
                // The resolver only performs a read-only ROM dump and stays fail-closed
                // unless the ROM structure and device identity are unambiguous.
                if (c.Profile != null && !c.MaxWritesReady && !_autoVbiosAttempted)
                {
                    _autoVbiosAttempted = true;
                    SetStatus(T("resolvingVbios"));
                    string[] dumped = new string[1];
                    OperationResult autoResult = await Task.Run(delegate { string d; OperationResult x = _power.TryAutoResolveVbios(out d); dumped[0] = d; return x; });
                    _lastAutoVbiosMessage = autoResult.Message ?? "";
                    AppLog.Write("Startup VBIOS resolver: " + (autoResult.Success ? "OK: " : "FAILED: ") + _lastAutoVbiosMessage);
                    if (autoResult.Success)
                        c = await Task.Run(delegate { return _power.CheckCompatibility(); });
                }

                PowerState p = null; int? ov = null; TunerState t = null;
                try { p = await Task.Run(delegate { return _power.GetPowerState(); }); } catch { }
                try { ov = await Task.Run(delegate { return _power.GetInstalledMaxOverrideTarget(); }); } catch { }
                try { t = await Task.Run(delegate { return NvApiTuner.Probe(c.Profile != null && c.Profile.AllowMsvdd); }); } catch { }
                _compat = c; if (t != null) _tuner = t;
                PopulatePowerTargets(c.Profile);
                UpdateHeader(); UpdatePowerUi(p, ov); UpdateCompatibilityUi(); UpdateTunerUi();
                SetStatus(c.Profile != null ? c.Profile.DisplayName + " — " + c.DriverVersion : T("unsupportedGpu"));
                if ((_settings.CoreOffsetEnabled || _settings.MemoryOffsetEnabled) && _tuner != null)
                {
                    bool needApply = (_settings.CoreOffsetEnabled && _tuner.CoreMHz.Supported && _tuner.CoreMHz.Current != _settings.CoreOffsetMHz)
                                  || (_settings.MemoryOffsetEnabled && _tuner.MemoryMHz.Supported && _tuner.MemoryMHz.Current != _settings.MemoryOffsetMHz);
                    if (needApply)
                    {
                        TuneRequest tr = new TuneRequest();
                        tr.SetCore = _settings.CoreOffsetEnabled && _tuner.CoreMHz.Supported;
                        tr.CoreMHz = _settings.CoreOffsetMHz;
                        tr.SetMemory = _settings.MemoryOffsetEnabled && _tuner.MemoryMHz.Supported;
                        tr.MemoryMHz = _settings.MemoryOffsetMHz;
                        bool allow = c.Profile != null && c.Profile.AllowMsvdd;
                        TunerState post = null;
                        OperationResult ocRes = await Task.Run(delegate { TunerState p2; OperationResult rr = NvApiTuner.Apply(tr, allow, out p2); post = p2; return rr; });
                        if (post != null) { _tuner = post; UpdateTunerUi(); }
                        AppLog.Write("Startup auto-apply OC result (Core " + (_settings.CoreOffsetEnabled ? (_settings.CoreOffsetMHz >= 0 ? "+" : "") + _settings.CoreOffsetMHz.ToString() + " MHz" : "stock")
                            + ", Mem " + (_settings.MemoryOffsetEnabled ? (_settings.MemoryOffsetMHz >= 0 ? "+" : "") + _settings.MemoryOffsetMHz.ToString() + " MHz" : "stock") + "): " + (ocRes.Success ? "OK" : ("FAIL: " + ocRes.Message)));
                    }
                }
                if (_settings.TelemetryEnabled && Visible && WindowState != FormWindowState.Minimized)
                    RefreshTelemetryAsync();
            }
            catch (Exception ex) { SetStatus(ex.Message); }
            finally { _busy = false; UpdateWriteButtons(); }
        }

        private async void RefreshTelemetryAsync()
        {
            if (!_settings.TelemetryEnabled) return;
            if (!Visible || WindowState == FormWindowState.Minimized) return;
            if (_telemetryBusy) return;
            _telemetryBusy = true;
            try
            {
                TelemetryState t = await Task.Run(delegate { return SystemProbe.GetTelemetry(); });
                if (!_settings.TelemetryEnabled) return;
                UpdateTelemetry(t);
            }
            catch { }
            finally { _telemetryBusy = false; }
        }

        private async void ForceRefreshTelemetryAsync()
        {
            if (_telemetryBusy) return;
            _telemetryBusy = true;
            try
            {
                TelemetryState t = await Task.Run(delegate { return SystemProbe.GetTelemetry(); });
                UpdateTelemetry(t);
            }
            catch { }
            finally { _telemetryBusy = false; }
        }

        private void UpdateHeader()
        {
            _gpuName.Text = !String.IsNullOrEmpty(_compat.GpuName) ? _compat.GpuName : "NVIDIA RTX Laptop GPU";
            string pci = _compat.Identity != null ? _compat.Identity.PciSummary() : "";
            _gpuSub.Text = "Driver " + (_compat.DriverVersion ?? "—") + "  ·  VBIOS " + (_compat.Vbios ?? "—") +
                           (!String.IsNullOrEmpty(pci) ? "  ·  " + pci : "");
            if (_compat.Profile == null) SetBadge(T("unsupportedGpu"), _danger);
            else if (_compat.Policy != null && !_compat.Policy.Consistent) SetBadge(T("policyLocked"), _danger);
            else if (_compat.CurrentWritesReady && _compat.MaxWritesReady) SetBadge(T("ready"), _ok);
            else if (!_compat.MaxWritesReady) SetBadge(T("vbiosNeeded"), _warning);
            else if (!_compat.CurrentWritesReady) SetBadge(T("needsValidation"), _warning);
            else SetBadge(T("limited"), _warning);
        }

        private void SetBadge(string text, Color color)
        {
            _compatBadge.Text = "● " + text; _compatBadge.BackColor = color; _compatBadge.ForeColor = Color.White;
        }

        private void PopulatePowerTargets(GpuProfile p)
        {
            if (_maxSlider == null || _currentSlider == null) return;
            if (p == null)
            {
                _maxSlider.Enabled = false;
                _currentSlider.Enabled = false;
                _maxTargetValue.Text = "—";
                _currentTargetValue.Text = "—";
                return;
            }

            int stockW = (_compat.VbiosResolver != null && _compat.VbiosResolver.StockMaxW > 0) ? _compat.VbiosResolver.StockMaxW : p.StockPowerW;
            int oldMax = _settings.MaxSelection;
            int oldCur = _settings.CurrentSelection;
            if (oldMax == 0 || !p.IsInRange(oldMax)) oldMax = stockW;
            if (oldCur == 0 || !p.IsInRange(oldCur)) oldCur = stockW;

            _maxSlider.SetRange(p.MinW, p.MaxPowerW, 5);
            _currentSlider.SetRange(p.MinW, p.MaxPowerW, 5);
            _maxSlider.Value = oldMax;
            _currentSlider.Value = oldCur;
            _maxSlider.Enabled = true;
            _currentSlider.Enabled = true;
            _settings.MaxSelection = oldMax;
            _settings.CurrentSelection = oldCur;
            if (_voltSlider != null)
            {
                _voltSlider.SetRange(900, 1150, 10);
                int oldVolt = _settings.VoltageSelection;
                if (oldVolt < 900 || oldVolt > 1150) oldVolt = 1000;
                _voltSlider.Value = oldVolt;
                _voltSlider.Enabled = true;
                _settings.VoltageSelection = oldVolt;
            }
            _powerRange.Text = T("profileRange") + ": " + p.MinW.ToString() + "–" + p.MaxPowerW.ToString() + " W";
            UpdatePowerTargetLabels();
            SaveSettings();
        }

        private void UpdatePowerTargetLabels()
        {
            if (_maxTargetValue != null && _maxSlider != null) _maxTargetValue.Text = _maxSlider.Value.ToString() + " W";
            if (_currentTargetValue != null && _currentSlider != null) _currentTargetValue.Text = _currentSlider.Value.ToString() + " W";
            UpdateVoltageTargetLabels();
        }

        private void UpdateVoltageTargetLabels()
        {
            if (_voltTargetValue != null && _voltSlider != null) _voltTargetValue.Text = _voltSlider.Value.ToString() + " mV";
            if (_voltStatus != null && _voltSlider != null)
            {
                if (_voltSlider.Value > 940)
                    _voltStatus.Text = "Target " + _voltSlider.Value.ToString() + " mV: Enforces boost clock to curve target point via NVML & unlocks 940 mV ceiling.";
                else
                    _voltStatus.Text = "Stock mobile voltage ceiling (940 mV VRel limit).";
            }
        }

        private void UpdatePowerUi(PowerState p, int? overrideTarget)
        {
            if (p == null) p = new PowerState();
            _powerCurrent.Text = W(p.CurrentW);
            _powerMax.Text = W(p.MaxW);

            string ov;
            if (!overrideTarget.HasValue) ov = T("overrideNone");
            else if (overrideTarget.Value == -1) ov = T("overrideUnknown");
            else ov = "romOverride: " + overrideTarget.Value.ToString() + " W";
            _powerOverride.Text = ov;

            UpdateExperimentalLabel();
            UpdateRebootBanner(p, overrideTarget);
        }

        private void UpdateExperimentalLabel()
        {
            GpuProfile p = _compat.Profile;
            if (p == null) { _powerExperimental.Text = ""; return; }
            int stockW = (_compat.VbiosResolver != null && _compat.VbiosResolver.StockMaxW > 0) ? _compat.VbiosResolver.StockMaxW : p.StockPowerW;
            int w = _currentSlider != null ? _currentSlider.Value : stockW;
            if (p.IsReferenceValidated(w))
                _powerExperimental.Text = T("referenceValidated");
            else
                _powerExperimental.Text = T("experimentalTarget");
        }

        private void UpdateRebootBanner(PowerState live, int? overrideTarget)
        {
            bool need = false; string msg = "";
            if (_compat.Profile != null)
            {
                int stockW = (_compat.VbiosResolver != null && _compat.VbiosResolver.StockMaxW > 0) ? _compat.VbiosResolver.StockMaxW : _compat.Profile.StockPowerW;
                if (overrideTarget.HasValue && overrideTarget.Value > 0 && live != null && live.MaxW.HasValue && Math.Abs(live.MaxW.Value - overrideTarget.Value) > 0.01)
                {
                    need = true;
                    msg = T("rebootApplyMax").Replace("{W}", overrideTarget.Value.ToString());
                }
                else if (!overrideTarget.HasValue && live != null && live.MaxW.HasValue && Math.Abs(live.MaxW.Value - stockW) > 0.01)
                {
                    need = true;
                    msg = T("rebootRestoreMax").Replace("{W}", stockW.ToString());
                }
            }
            _rebootBanner.Visible = need;
            _rebootText.Text = msg;
        }

        private void UpdateCompatibilityUi()
        {
            if (_compatGpuState != null)
            {
                string pci = _compat.Identity != null ? _compat.Identity.PciSummary() : "";
                _compatGpuState.Text = (_compat.Profile != null ? _compat.Profile.DisplayName : T("unsupportedGpu")) +
                    (!String.IsNullOrEmpty(pci) ? "\r\n" + pci : "");
                _compatGpuState.ForeColor = _compat.Profile != null ? _ok : _danger;
            }
            if (_compatDriverState != null)
            {
                _compatDriverState.Text = _compat.Driver != null && _compat.Driver.Trusted ?
                    "READY · RVA 0x" + _compat.Driver.TransportRva.ToString("X") :
                    (_compat.Driver != null && _compat.Driver.CandidateFound ? "FOUND · " + T("validationRequired") : "UNRESOLVED");
                _compatDriverState.ForeColor = _compat.Driver != null && _compat.Driver.Trusted ? _ok : _warning;
            }
            if (_compatVbiosState != null)
            {
                _compatVbiosState.Text = _compat.VbiosResolver != null && _compat.VbiosResolver.Resolved ?
                    "READY · 0x" + _compat.VbiosResolver.ShadowOffset.ToString("X") + "\r\nscore " + _compat.VbiosResolver.SemanticScore.ToString() : "UNRESOLVED";
                _compatVbiosState.ForeColor = _compat.VbiosResolver != null && _compat.VbiosResolver.Resolved ? _ok : _warning;
            }
            if (_compatPolicyState != null)
            {
                _compatPolicyState.Text = _compat.Policy != null ? _compat.Policy.State + "\r\n" + (_compat.Policy.Reason ?? "") : "UNRESOLVED";
                _compatPolicyState.ForeColor = _compat.Policy != null && _compat.Policy.Consistent ? _ok : _danger;
            }
            _compatText.Text = BuildCompatibilityText();
            UpdateWriteButtons();
        }

        private string BuildCompatibilityText()
        {
            string s = "NvpwrControl: " + PowerBackend.Version + "\r\n\r\n";
            s += "GPU: " + (_compat.GpuName ?? "") + "\r\n";
            s += "Profile: " + (_compat.Profile != null ? _compat.Profile.ToString() : "UNSUPPORTED") + "\r\n";
            s += "GPU PnP: " + (_compat.GpuPnpId ?? "") + "\r\n";
            if (_compat.Identity != null)
            {
                s += "PCI identity: " + _compat.Identity.PciSummary() + "\r\n";
                s += "Detection: " + (_compat.Identity.Detection ?? "") + "\r\n";
            }
            s += "VBIOS: " + (_compat.Vbios ?? "") + "\r\n";
            s += "Driver: " + (_compat.DriverVersion ?? "") + "\r\n";
            s += "KMD SHA256: " + (_compat.KmdSha256 ?? "") + "\r\n";
            s += "NVAPI impl SHA256: " + (_compat.ImplSha256 ?? "") + "\r\n\r\n";

            if (_compat.Driver != null)
            {
                s += "DRIVER RESOLVER\r\n";
                s += "  candidate: " + _compat.Driver.CandidateFound.ToString() + "\r\n";
                s += "  trusted: " + _compat.Driver.Trusted.ToString() + "\r\n";
                s += "  source: " + (_compat.Driver.Source ?? "") + "\r\n";
                s += "  transport RVA: 0x" + _compat.Driver.TransportRva.ToString("X") + "\r\n";
                s += "  reason: " + (_compat.Driver.Reason ?? "") + "\r\n\r\n";
            }

            if (_compat.VbiosResolver != null)
            {
                s += "VBIOS RESOLVER\r\n";
                s += "  resolved: " + _compat.VbiosResolver.Resolved.ToString() + "\r\n";
                s += "  source: " + (_compat.VbiosResolver.Source ?? "") + "\r\n";
                s += "  stock MAX: " + _compat.VbiosResolver.StockMaxW.ToString() + " W\r\n";
                s += "  shadow MAX offset: 0x" + _compat.VbiosResolver.ShadowOffset.ToString("X") + "\r\n";
                s += "  Power Budget table raw: 0x" + _compat.VbiosResolver.PowerTableRawOffset.ToString("X") + "\r\n";
                s += "  MAX field raw: 0x" + _compat.VbiosResolver.MaxFieldRawOffset.ToString("X") + "\r\n";
                s += "  legacy image raw: 0x" + _compat.VbiosResolver.LegacyImageRawOffset.ToString("X") + "\r\n";
                s += "  PCIR device: 0x" + _compat.VbiosResolver.PcirDeviceId.ToString("X4") + "\r\n";
                s += "  legacy images: " + _compat.VbiosResolver.RomImageCount.ToString() + " / matching device: " + _compat.VbiosResolver.MatchingRomImageCount.ToString() + "\r\n";
                s += "  power candidates: " + _compat.VbiosResolver.CandidateCount.ToString() + " / entry index: " + _compat.VbiosResolver.EntryIndex.ToString() + "\r\n";
                s += "  layout: " + (_compat.VbiosResolver.Layout ?? "") + "\r\n";
                s += "  record min/default/max/base: " + _compat.VbiosResolver.RecordMinMw.ToString() + "/" + _compat.VbiosResolver.RecordDefaultMw.ToString() + "/" + _compat.VbiosResolver.RecordMaxMw.ToString() + "/" + _compat.VbiosResolver.RecordBaseMw.ToString() + " mW\r\n";
                s += "  semantic score/confidence: " + _compat.VbiosResolver.SemanticScore.ToString() + " / " + (_compat.VbiosResolver.Confidence ?? "") + "\r\n";
                s += "  reason: " + (_compat.VbiosResolver.Reason ?? "") + "\r\n";
                if (!String.IsNullOrEmpty(_lastAutoVbiosMessage))
                    s += "  startup auto-resolver: " + _lastAutoVbiosMessage + "\r\n";
                s += "\r\n";
            }

            if (_compat.Policy != null)
            {
                s += "POWER POLICY STATE\r\n";
                s += "  state: " + (_compat.Policy.State ?? "") + "\r\n";
                s += "  consistent: " + _compat.Policy.Consistent.ToString() + "\r\n";
                s += "  live CURRENT/MAX: " + (_compat.Policy.LiveCurrentW.HasValue ? _compat.Policy.LiveCurrentW.Value.ToString("0.00") : "N/A") + " / " + (_compat.Policy.LiveMaxW.HasValue ? _compat.Policy.LiveMaxW.Value.ToString("0.00") : "N/A") + " W\r\n";
                s += "  installed override: " + (_compat.Policy.InstalledOverrideW.HasValue ? _compat.Policy.InstalledOverrideW.Value.ToString() : "none") + "\r\n";
                s += "  reason: " + (_compat.Policy.Reason ?? "") + "\r\n\r\n";
            }

            s += "CURRENT writes ready: " + _compat.CurrentWritesReady.ToString() + "\r\n";
            s += "MAX writes ready: " + _compat.MaxWritesReady.ToString() + "\r\n\r\n";
            s += T("compatSafetyText");
            return s;
        }

        private void UpdateTelemetry(TelemetryState t)
        {
            if (t == null) return;
            _headCore.Text = D(t.CoreClockMHz, " MHz");
            _headVolt.Text = D(t.VoltageV, " V");
            _headPower.Text = D(t.PowerW, " W");
            _headTemp.Text = D(t.GpuTempC, " °C");
            _headHotspot.Text = D(t.HotspotTempC, " °C");

            _monPower.Text = D(t.PowerW, " W");
            _monTemp.Text = D(t.GpuTempC, " °C");
            _monHotspot.Text = D(t.HotspotTempC, " °C");
            _monMemTemp.Text = D(t.MemoryTempC, " °C");
            _monUtil.Text = D(t.UtilizationPct, " %");
            _monCore.Text = D(t.CoreClockMHz, " MHz");
            _monMem.Text = D(t.MemoryClockMHz, " MHz");
            _monCurrent.Text = W(t.Power != null ? t.Power.CurrentW : null);
            _monMax.Text = W(t.Power != null ? t.Power.MaxW : null);
            if (_powerVoltage != null && t.VoltageV.HasValue) _powerVoltage.Text = D(t.VoltageV, " V");
        }

        private void UpdateTunerUi()
        {
            ApplyTuneRange(_core, _coreEnable, _coreRange, _tuner.CoreMHz, " MHz");
            ApplyTuneRange(_mem, _memEnable, _memRange, _tuner.MemoryMHz, " MHz");
            ApplyTuneRange(_nvvdd, _nvvddEnable, _nvvddRange, _tuner.NvvddMv, " mV");

            if (_coreEnable.Enabled && _settings.CoreOffsetEnabled)
            {
                _coreEnable.Checked = true;
                SetNumeric(_core, _settings.CoreOffsetMHz);
            }
            if (_memEnable.Enabled && _settings.MemoryOffsetEnabled)
            {
                _memEnable.Checked = true;
                SetNumeric(_mem, _settings.MemoryOffsetMHz);
            }

            _xbar.Enabled = _tuner.XbarWritable; _xbarEnable.Enabled = _tuner.XbarWritable;
            if (_tuner.XbarWritable) SetNumeric(_xbar, _tuner.XbarMHz);
            _xbarRange.Text = _tuner.XbarWritable ? (_tuner.XbarMinMHz.ToString() + ".." + _tuner.XbarMaxMHz.ToString() + " MHz") : "N/A";

            _msvdd.Enabled = _tuner.MsvddWritable; _msvddEnable.Enabled = _tuner.MsvddWritable;
            if (_tuner.MsvddWritable) SetNumeric(_msvdd, _tuner.MsvddMv);
            _msvddRange.Text = _tuner.MsvddWritable ? (_tuner.MsvddMinMv.ToString() + ".." + _tuner.MsvddMaxMv.ToString() + " mV") : "N/A";

            _ratio.Enabled = _tuner.RatioWritable; _ratioEnable.Enabled = _tuner.RatioWritable;
            if (_tuner.RatioWritable) SetNumeric(_ratio, (decimal)_tuner.GpcXbarRatio);
            _ratioRange.Text = _tuner.RatioWritable ? "0.0..2.0" : "N/A";

            _tuneSummary.Text =
                "XBAR physical: " + (_tuner.XbarPhysicalMHz != 0 ? _tuner.XbarPhysicalMHz.ToString() + " MHz" : "N/A") + "\r\n" +
                "V/F: " + (_tuner.VfInfoAvailable ? "available" : "N/A") + "\r\n" +
                "ADC/rail: " + (_tuner.AdcInfoAvailable ? "available" : "N/A") + "\r\n" +
                "XBAR layout: " + (_tuner.XbarWritable ? "base 0x" + _tuner.XbarEntryBase.ToString("X") + ", stride 0x" + _tuner.XbarEntryStride.ToString("X") + ", index " + _tuner.XbarDomainIndex.ToString() : "N/A") + "\r\n" +
                (_tuner.Error ?? "");

            UpdateWriteButtons();
        }

        private void ApplyTuneRange(NumericUpDown n, CheckBox cb, Label l, TuneRange r, string unit)
        {
            n.Enabled = r.Supported; cb.Enabled = r.Supported;
            if (!r.Supported) { l.Text = "N/A"; cb.Checked = false; return; }
            n.Minimum = r.Min; n.Maximum = r.Max; SetNumeric(n, r.Current);
            l.Text = T("applied") + " " + r.Current.ToString() + unit + "  ·  " + T("allowed") + " " + r.Min.ToString() + ".." + r.Max.ToString() + unit;
        }

        private async void ApplyMaxAsync()
        {
            if (_busy) return;
            int stockW = (_compat.VbiosResolver != null && _compat.VbiosResolver.StockMaxW > 0) ? _compat.VbiosResolver.StockMaxW : (_compat.Profile != null ? _compat.Profile.StockPowerW : 0);
            int w = _maxSlider != null ? _maxSlider.Value : stockW;
            if (_compat.Profile == null) return;
            if (!_compat.Profile.IsReferenceValidated(w) && !ConfirmExperimental(w)) return;
            if (!_compat.MaxWritesReady)
            {
                MessageBox.Show(this, T("resolveVbiosFirst"), "NvpwrControl", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ShowPage("compatibility"); return;
            }
            await RunOperationTask(delegate { return _power.SetMaxOverride(w); }, true);
        }

        private async void ApplyCurrentAsync()
        {
            if (_busy) return;
            int stockW = (_compat.VbiosResolver != null && _compat.VbiosResolver.StockMaxW > 0) ? _compat.VbiosResolver.StockMaxW : (_compat.Profile != null ? _compat.Profile.StockPowerW : 0);
            int w = _currentSlider != null ? _currentSlider.Value : stockW;
            if (_compat.Profile == null) return;
            if (!_compat.Profile.IsReferenceValidated(w) && !ConfirmExperimental(w)) return;
            if (!_compat.CurrentWritesReady)
            {
                MessageBox.Show(this, T("validateDriverFirst"), "NvpwrControl", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ShowPage("compatibility"); return;
            }
            await RunOperationTask(delegate {
                OperationResult r = _power.SetCurrent(w);
                if (r.Success)
                {
                    _settings.CurrentSelection = w;
                    SettingsStore.Save(_settings);
                    EnsureMsiSyncRunning();
                }
                return r;
            }, true);
        }

        private void EnsureMsiSyncRunning()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string syncExe = Path.Combine(baseDir, "MsiAfterburnerSync.exe");
                if (!File.Exists(syncExe))
                    syncExe = Path.Combine(baseDir, "..", "MsiAfterburnerSync.exe");
                if (!File.Exists(syncExe))
                    syncExe = Path.Combine(baseDir, "MsiAfterburnerSync", "dist", "MsiAfterburnerSync.exe");
                if (!File.Exists(syncExe))
                    syncExe = Path.Combine(baseDir, "..", "MsiAfterburnerSync", "dist", "MsiAfterburnerSync.exe");
                if (!File.Exists(syncExe))
                    syncExe = Path.Combine(baseDir, "..", "..", "MsiAfterburnerSync.exe");
                if (!File.Exists(syncExe))
                    syncExe = Path.Combine(baseDir, "..", "..", "MsiAfterburnerSync", "dist", "MsiAfterburnerSync.exe");

                if (File.Exists(syncExe))
                {
                    syncExe = Path.GetFullPath(syncExe);
                    string userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                    string ps =
                        "$a=New-ScheduledTaskAction -Execute '" + syncExe.Replace("'", "''") + "' -Argument '--minimized';" +
                        "$t=New-ScheduledTaskTrigger -AtLogOn;" +
                        "$p=New-ScheduledTaskPrincipal -UserId '" + userName.Replace("'", "''") + "' -LogonType Interactive -RunLevel Highest;" +
                        "$s=New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0;" +
                        "Register-ScheduledTask -TaskName 'MsiAfterburnerSync' -Action $a -Trigger $t -Principal $p -Settings $s -Force | Out-Null;";
                    
                    string encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(ps));
                    int rc;
                    SystemProbe.RunProcess("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded, 10000, out rc);

                    if (System.Diagnostics.Process.GetProcessesByName("MsiAfterburnerSync").Length == 0)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = syncExe,
                            Arguments = "--minimized",
                            UseShellExecute = true
                        });
                        AppLog.Write("Started MsiAfterburnerSync background watcher: " + syncExe);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("EnsureMsiSyncRunning error: " + ex.Message);
            }
        }

        private async void RestoreCurrentAsync()
        {
            if (_compat.Profile == null) return;
            int stock = (_compat.VbiosResolver != null && _compat.VbiosResolver.StockMaxW > 0) ? _compat.VbiosResolver.StockMaxW : _compat.Profile.StockPowerW;
            await RunOperationTask(delegate { return _power.SetCurrent(stock); }, true);
        }

        private void HandleMsiScenarioChanged()
        {
            if (!_settings.MsiAutoApply) return;
            if (_compat == null || _compat.Profile == null || !_compat.CurrentWritesReady)
            {
                try { _compat = _power.CheckCompatibility(); } catch { }
                if (_compat == null || _compat.Profile == null || !_compat.CurrentWritesReady) return;
            }

            int targetW = _settings.CurrentSelection > 0 ? _settings.CurrentSelection : 0;
            string statusMsg = "";
            if (targetW > 0)
            {
                AppLog.Write("MSI Center scenario change detected (" + _settings.MsiTimeoutSec.ToString() + "s timeout). Auto-reapplying CURRENT " + targetW.ToString() + " W...");
                OperationResult r = _power.SetCurrent(targetW);
                AppLog.Write("MSI Center auto-reapply power result: " + (r.Success ? "OK" : ("FAIL: " + r.Message)));
                statusMsg = r.Success ? ("Reapplied " + targetW.ToString() + " W") : ("Power FAIL: " + r.Message);
            }

            if (_settings.CoreOffsetEnabled || _settings.MemoryOffsetEnabled)
            {
                TuneRequest tr = new TuneRequest();
                tr.SetCore = _settings.CoreOffsetEnabled;
                tr.CoreMHz = _settings.CoreOffsetMHz;
                tr.SetMemory = _settings.MemoryOffsetEnabled;
                tr.MemoryMHz = _settings.MemoryOffsetMHz;
                bool allow = _compat.Profile != null && _compat.Profile.AllowMsvdd;
                TunerState postState;
                OperationResult ocRes = NvApiTuner.Apply(tr, allow, out postState);
                if (postState != null) _tuner = postState;
                AppLog.Write("MSI Center auto-reapply OC result (Core " 
                    + (_settings.CoreOffsetEnabled ? (_settings.CoreOffsetMHz >= 0 ? "+" : "") + _settings.CoreOffsetMHz.ToString() + " MHz" : "stock")
                    + ", Mem " + (_settings.MemoryOffsetEnabled ? (_settings.MemoryOffsetMHz >= 0 ? "+" : "") + _settings.MemoryOffsetMHz.ToString() + " MHz" : "stock") 
                    + "): " + (ocRes.Success ? "OK" : ("FAIL: " + ocRes.Message)));

                if (!String.IsNullOrEmpty(statusMsg)) statusMsg += " · ";
                statusMsg += ocRes.Success
                    ? ("OC Core " + (_settings.CoreOffsetEnabled ? (_settings.CoreOffsetMHz >= 0 ? "+" : "") + _settings.CoreOffsetMHz.ToString() + " MHz" : "stock")
                       + ", Mem " + (_settings.MemoryOffsetEnabled ? (_settings.MemoryOffsetMHz >= 0 ? "+" : "") + _settings.MemoryOffsetMHz.ToString() + " MHz" : "stock") + " reapplied")
                    : ("OC FAIL: " + ocRes.Message);
            }

            if (_trayIcon != null && !String.IsNullOrEmpty(statusMsg))
            {
                try
                {
                    _trayIcon.ShowBalloonTip(3000, "NvpwrControl", "MSI Center mode change · " + statusMsg, ToolTipIcon.Info);
                }
                catch { }
            }

            if (IsHandleCreated && !String.IsNullOrEmpty(statusMsg))
            {
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        SetStatus("MSI Center mode change · " + statusMsg);
                        UpdateTunerUi();
                        if (_settings.TelemetryEnabled && Visible && WindowState != FormWindowState.Minimized)
                            RefreshTelemetryAsync();
                    }));
                }
                catch { }
            }
        }

        private async void RestoreMaxAsync()
        {
            await RunOperationTask(delegate { return _power.RemoveMaxOverride(); }, true);
        }

        private async void ApplyVoltageAsync()
        {
            if (_busy) return;
            int mv = _voltSlider != null ? _voltSlider.Value : 1000;
            await RunOperationTask(delegate { return _power.SetVoltage(mv); }, true);
            UpdateVoltageTargetLabels();
        }

        private async void RestoreVoltageAsync()
        {
            if (_busy) return;
            await RunOperationTask(delegate { return _power.RestoreVoltage(); }, true);
        }

        private async void SaveAutostartAsync()
        {
            int stockW = (_compat.VbiosResolver != null && _compat.VbiosResolver.StockMaxW > 0) ? _compat.VbiosResolver.StockMaxW : (_compat.Profile != null ? _compat.Profile.StockPowerW : 0);
            int w = _currentSlider != null ? _currentSlider.Value : stockW;
            await RunOperationTask(delegate { return _power.InstallAutostart(w, Application.ExecutablePath); }, true);
        }

        private async void ValidateDriverAsync()
        {
            await RunOperationTask(delegate { return _power.ValidateDriverResolver(); }, true);
        }

        private async void ResolveVbiosAutoAsync()
        {
            if (_busy) return; _busy = true; UpdateWriteButtons(); SetStatus(T("resolvingVbios"));
            try
            {
                string[] dumped = new string[1];
                OperationResult r = await Task.Run(delegate { string d; OperationResult x = _power.TryAutoResolveVbios(out d); dumped[0] = d; return x; });
                ShowResult(r);
                if (!r.Success && r.Message.IndexOf("Select a .rom", StringComparison.OrdinalIgnoreCase) >= 0) SelectRomAndResolveAsync();
            }
            catch (Exception ex)
            {
                ShowResult(OperationResult.Fail(ex.Message));
            }
            finally
            {
                _busy = false;
            }
            await RefreshAfterOperation();
        }

        private async void SelectRomAndResolveAsync()
        {
            OpenFileDialog d = new OpenFileDialog { Filter = "VBIOS ROM (*.rom;*.bin)|*.rom;*.bin|All files|*.*", Title = T("selectRomTitle") };
            if (d.ShowDialog(this) != DialogResult.OK) return;
            await RunOperationTask(delegate { return _power.ResolveVbiosFromRom(d.FileName); }, true);
        }

        private async void ProbeTuningAsync()
        {
            if (_busy) return; _busy = true; UpdateWriteButtons();
            try
            {
                bool allow = _compat.Profile != null && _compat.Profile.AllowMsvdd;
                _tuner = await Task.Run(delegate { return NvApiTuner.Probe(allow); });
                UpdateTunerUi();
            }
            finally { _busy = false; UpdateWriteButtons(); }
        }

        private async void ApplyTuningAsync()
        {
            if (_busy) return;
            TuneRequest r = new TuneRequest();
            r.SetCore = (_coreEnable.Checked || _core.Value != 0) && _coreEnable.Enabled;
            if (r.SetCore) _coreEnable.Checked = true;
            r.CoreMHz = (int)_core.Value;

            r.SetMemory = (_memEnable.Checked || _mem.Value != 0) && _memEnable.Enabled;
            if (r.SetMemory) _memEnable.Checked = true;
            r.MemoryMHz = (int)_mem.Value;

            r.SetXbar = (_xbarEnable.Checked || _xbar.Value != 0) && _xbarEnable.Enabled;
            if (r.SetXbar) _xbarEnable.Checked = true;
            r.XbarMHz = (int)_xbar.Value;

            r.SetMsvdd = (_msvddEnable.Checked || _msvdd.Value != 0) && _msvddEnable.Enabled;
            if (r.SetMsvdd) _msvddEnable.Checked = true;
            r.MsvddMv = (int)_msvdd.Value;

            r.SetNvvdd = (_nvvddEnable.Checked || _nvvdd.Value != 0) && _nvvddEnable.Enabled;
            if (r.SetNvvdd) _nvvddEnable.Checked = true;
            r.NvvddMv = (int)_nvvdd.Value;

            r.SetRatio = (_ratioEnable.Checked || _ratio.Value != 0m) && _ratioEnable.Enabled;
            if (r.SetRatio) _ratioEnable.Checked = true;
            r.GpcXbarRatio = (double)_ratio.Value;
            bool allow = _compat.Profile != null && _compat.Profile.AllowMsvdd;

            _busy = true; UpdateWriteButtons();
            try
            {
                TunerState[] post = new TunerState[1];
                OperationResult x = await Task.Run(delegate { TunerState p2; OperationResult rr = NvApiTuner.Apply(r, allow, out p2); post[0] = p2; return rr; });
                if (post[0] != null) _tuner = post[0]; UpdateTunerUi(); ShowResult(x);
                if (x.Success)
                {
                    _settings.CoreOffsetEnabled = _coreEnable.Checked && _coreEnable.Enabled;
                    _settings.CoreOffsetMHz = (int)_core.Value;
                    _settings.MemoryOffsetEnabled = _memEnable.Checked && _memEnable.Enabled;
                    _settings.MemoryOffsetMHz = (int)_mem.Value;
                    SaveSettings();
                }
            }
            finally { _busy = false; UpdateWriteButtons(); }
        }

        private async void ResetTuningAsync()
        {
            bool allow = _compat.Profile != null && _compat.Profile.AllowMsvdd;
            _busy = true; UpdateWriteButtons();
            try
            {
                TunerState[] post = new TunerState[1];
                OperationResult x = await Task.Run(delegate { TunerState p2; OperationResult rr = NvApiTuner.ResetFactory(allow, out p2); post[0] = p2; return rr; });
                if (post[0] != null) _tuner = post[0]; UpdateTunerUi(); ShowResult(x);
                if (x.Success)
                {
                    _settings.CoreOffsetEnabled = false;
                    _settings.CoreOffsetMHz = 0;
                    _settings.MemoryOffsetEnabled = false;
                    _settings.MemoryOffsetMHz = 0;
                    SaveSettings();
                }
            }
            finally { _busy = false; UpdateWriteButtons(); }
        }

        private async void FactoryResetAsync()
        {
            if (_busy || _compat.Profile == null) return;
            DialogResult q = MessageBox.Show(this, T("factoryConfirm"), "NvpwrControl", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (q != DialogResult.Yes) return;

            _busy = true; UpdateWriteButtons();
            try
            {
                List<string> msgs = new List<string>(); bool ok = true; bool reboot = false;
                bool allow = _compat.Profile.AllowMsvdd;
                TunerState[] post = new TunerState[1];
                OperationResult t = await Task.Run(delegate { TunerState p2; OperationResult rr = NvApiTuner.ResetFactory(allow, out p2); post[0] = p2; return rr; }); msgs.Add("Tuning: " + t.Message); ok &= t.Success;
                if (t.Success)
                {
                    _settings.CoreOffsetEnabled = false;
                    _settings.CoreOffsetMHz = 0;
                    _settings.MemoryOffsetEnabled = false;
                    _settings.MemoryOffsetMHz = 0;
                    SaveSettings();
                }
                if (_compat.CurrentWritesReady)
                {
                    int stockW = (_compat.VbiosResolver != null && _compat.VbiosResolver.StockMaxW > 0) ? _compat.VbiosResolver.StockMaxW : _compat.Profile.StockPowerW;
                    OperationResult cur = await Task.Run(delegate { return _power.SetCurrent(stockW); }); msgs.Add("CURRENT: " + cur.Message); ok &= cur.Success;
                }
                OperationResult auto = await Task.Run(delegate { return _power.RemoveAutostart(); }); msgs.Add("Autostart: " + auto.Message); ok &= auto.Success;
                if (_compat.MaxWritesReady)
                {
                    OperationResult max = await Task.Run(delegate { return _power.RemoveMaxOverride(); }); msgs.Add("MAX: " + max.Message); ok &= max.Success; reboot |= max.RebootRequired;
                }
                OperationResult all = ok ? OperationResult.Ok(String.Join("\r\n", msgs.ToArray())) : OperationResult.Fail(String.Join("\r\n", msgs.ToArray())); all.RebootRequired = reboot;
                ShowResult(all);
            }
            catch (Exception ex)
            {
                ShowResult(OperationResult.Fail(ex.Message));
            }
            finally
            {
                _busy = false;
            }
            await RefreshAfterOperation();
        }

        private async void RestartNvidiaAsync()
        {
            if (_compat.Profile == null) return;
            DialogResult q = MessageBox.Show(this, T("restartNvWarn"), "NvpwrControl", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (q != DialogResult.Yes) return;
            await RunOperationTask(delegate { return SystemProbe.RestartNvidiaDevice(_compat.GpuName); }, true);
        }

        private void RebootNow()
        {
            if (MessageBox.Show(this, T("rebootConfirm"), "NvpwrControl", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            OperationResult r = SystemProbe.RebootWindowsNow(); if (!r.Success) ShowResult(r);
        }

        private async Task RunOperationTask(Func<OperationResult> fn, bool refresh)
        {
            if (_busy) return; _busy = true; UpdateWriteButtons();
            try
            {
                OperationResult r = await Task.Run(fn);
                ShowResult(r);
            }
            catch (Exception ex)
            {
                ShowResult(OperationResult.Fail(ex.Message));
            }
            finally
            {
                _busy = false;
            }

            if (refresh)
            {
                await RefreshAfterOperation();
            }
            else
            {
                UpdateWriteButtons();
            }
        }

        private async void RunOperationAsync(Func<OperationResult> fn, bool refresh)
        {
            await RunOperationTask(fn, refresh);
        }

        private async Task RefreshAfterOperation()
        {
            CompatibilityState c = await Task.Run(delegate { return _power.CheckCompatibility(); });
            PowerState p = null; int? ov = null;
            try { p = await Task.Run(delegate { return _power.GetPowerState(); }); } catch { }
            try { ov = await Task.Run(delegate { return _power.GetInstalledMaxOverrideTarget(); }); } catch { }
            _compat = c; PopulatePowerTargets(c.Profile); UpdateHeader(); UpdatePowerUi(p, ov); UpdateCompatibilityUi(); UpdateWriteButtons();
        }

        private bool ConfirmExperimental(int watts)
        {
            if (_compat.Profile == null || _compat.Profile.IsReferenceValidated(watts)) return true;
            string text = T("experimentalConfirm").Replace("{W}", watts.ToString()).Replace("{GPU}", _compat.Profile.DisplayName);
            return MessageBox.Show(this, text, "NvpwrControl", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        private void UpdateWriteButtons()
        {
            bool idle = !_busy;
            _applyMax.Enabled = idle && _compat.Profile != null && _compat.MaxWritesReady;
            _restoreMax.Enabled = idle && _compat.Profile != null && _compat.MaxWritesReady;
            _applyCurrent.Enabled = idle && _compat.Profile != null && _compat.CurrentWritesReady;
            _restoreCurrent.Enabled = idle && _compat.Profile != null && _compat.CurrentWritesReady;
            if (_applyVolt != null) _applyVolt.Enabled = idle && _compat.Profile != null;
            if (_restartDriverVolt != null) _restartDriverVolt.Enabled = idle && _compat.Profile != null;
            if (_restoreVolt != null) _restoreVolt.Enabled = idle && _compat.Profile != null;
            _autostart.Enabled = idle && _compat.Profile != null && _compat.CurrentWritesReady;
            _removeAutostart.Enabled = idle;
            _validateDriver.Enabled = idle && _compat.Driver != null && _compat.Driver.CandidateFound && !_compat.Driver.Trusted;
            _autoVbios.Enabled = idle && _compat.Profile != null && !_compat.MaxWritesReady;
            _selectRom.Enabled = idle && _compat.Profile != null;
            bool anyTune = (_coreEnable != null && _coreEnable.Enabled && (_coreEnable.Checked || _core.Value != 0)) ||
                           (_memEnable != null && _memEnable.Enabled && (_memEnable.Checked || _mem.Value != 0)) ||
                           (_xbarEnable != null && _xbarEnable.Enabled && (_xbarEnable.Checked || _xbar.Value != 0)) ||
                           (_msvddEnable != null && _msvddEnable.Enabled && (_msvddEnable.Checked || _msvdd.Value != 0)) ||
                           (_nvvddEnable != null && _nvvddEnable.Enabled && (_nvvddEnable.Checked || _nvvdd.Value != 0)) ||
                           (_ratioEnable != null && _ratioEnable.Enabled && (_ratioEnable.Checked || _ratio.Value != 0m));
            _applyTune.Enabled = idle && anyTune;
            _probeTune.Enabled = idle; _resetTune.Enabled = idle; _resetAll.Enabled = idle;
        }

        private void ShowPage(string page)
        {
            _settings.LastPage = page; SaveSettings();
            _pagePower.Visible = page == "power"; _pageTune.Visible = page == "tuning"; _pageTelemetry.Visible = page == "telemetry"; _pageCompat.Visible = page == "compatibility"; _pageSettings.Visible = page == "settings";
            _pagePower.BringToFront(); if (page == "tuning") _pageTune.BringToFront(); if (page == "telemetry") _pageTelemetry.BringToFront(); if (page == "compatibility") _pageCompat.BringToFront(); if (page == "settings") _pageSettings.BringToFront();
            SetNavActive(_navPower, page == "power"); SetNavActive(_navTune, page == "tuning"); SetNavActive(_navTelemetry, page == "telemetry"); SetNavActive(_navCompat, page == "compatibility"); SetNavActive(_navSettings, page == "settings");
        }

        private void SetNavActive(NavButton b, bool active) { b.Active = active; b.Invalidate(); }

        private void ApplyTheme()
        {
            bool light = _settings.Theme == "light";
            bool midnight = _settings.Theme == "midnight";
            bool dark = !light;

            if (light)
            {
                _bg = Color.FromArgb(238, 242, 247);
                _card = Color.White;
                _card2 = Color.FromArgb(248, 250, 253);
                _fg = Color.FromArgb(24, 31, 40);
                _muted = Color.FromArgb(95, 105, 118);
                _border = Color.FromArgb(200, 208, 219);
            }
            else if (midnight)
            {
                _bg = Color.FromArgb(5, 9, 16);
                _card = Color.FromArgb(9, 15, 24);
                _card2 = Color.FromArgb(13, 21, 33);
                _fg = Color.FromArgb(238, 243, 250);
                _muted = Color.FromArgb(139, 153, 173);
                _border = Color.FromArgb(40, 55, 74);
            }
            else
            {
                _bg = Color.FromArgb(7, 9, 12);
                _card = Color.FromArgb(10, 13, 17);
                _card2 = Color.FromArgb(14, 18, 23);
                _fg = Color.FromArgb(238, 241, 245);
                _muted = Color.FromArgb(145, 157, 171);
                _border = Color.FromArgb(43, 55, 68);
            }

            _accentColor = AccentColor(_settings.Accent);
            _buttonColor = AccentColor(_settings.ButtonAccent);
            _sliderColor = AccentColor(_settings.SliderAccent);
            _danger = Color.FromArgb(213, 74, 74);
            _ok = Color.FromArgb(47, 181, 110);
            _warning = Color.FromArgb(225, 153, 51);

            BackColor = _bg; ForeColor = _fg;
            ThemeRecursive(this, dark);
            _header.BackColor = _bg; _nav.BackColor = _bg; _status.BackColor = _card; _status.ForeColor = _fg;
            _rebootBanner.BackColor = dark ? Color.FromArgb(45, 35, 15) : Color.FromArgb(255, 244, 207);
            _rebootBanner.ForeColor = dark ? Color.FromArgb(255, 220, 130) : Color.FromArgb(105, 75, 0);
            if (_compatText != null) { _compatText.BackColor = dark ? Color.FromArgb(5, 8, 11) : Color.White; _compatText.ForeColor = _fg; }
            if (_compatText != null) UpdateCompatibilityUi();
            Invalidate(true);
        }

        private void ThemeRecursive(Control c, bool dark)
        {
            c.ForeColor = _fg;
            if (c is Panel || c is FlowLayoutPanel || c is TableLayoutPanel || c == this) c.BackColor = _bg;

            CardPanel card = c as CardPanel;
            if (card != null)
            {
                card.BackColor = _card;
                card.BorderColor = _border;
                card.AccentColor = _accentColor;
            }

            NavButton nav = c as NavButton;
            if (nav != null)
            {
                nav.BackColor = _bg;
                nav.ForeColor = _fg;
                nav.AccentColor = _accentColor;
                nav.HoverColor = _card2;
            }

            ThemedButton tb = c as ThemedButton;
            if (tb != null)
            {
                tb.AccentColor = _buttonColor;
                tb.SurfaceColor = _card2;
                tb.BorderColor = _border;
                tb.TextColor = _fg;
                tb.MutedTextColor = _muted;
            }
            else
            {
                Button b = c as Button;
                if (b != null && nav == null)
                {
                    b.BackColor = _card2;
                    b.ForeColor = _fg;
                    b.FlatAppearance.BorderColor = _border;
                }
            }

            PowerSlider slider = c as PowerSlider;
            if (slider != null)
            {
                slider.BackColor = _card;
                slider.TrackColor = dark ? Color.FromArgb(48, 59, 72) : Color.FromArgb(207, 214, 224);
                slider.FillColor = _sliderColor;
                slider.KnobColor = lightColor(dark);
                slider.TickColor = _muted;
                slider.LabelColor = _muted;
            }

            ComboBox cb = c as ComboBox; if (cb != null) { cb.BackColor = _card2; cb.ForeColor = _fg; }
            NumericUpDown nu = c as NumericUpDown; if (nu != null) { nu.BackColor = _card2; nu.ForeColor = _fg; }
            CheckBox ch = c as CheckBox; if (ch != null) ch.ForeColor = _fg;
            if (c.Name == "accentLine") c.BackColor = _accentColor;
            foreach (Control x in c.Controls) ThemeRecursive(x, dark);
        }

        private Color lightColor(bool dark)
        {
            return dark ? Color.FromArgb(244, 247, 251) : Color.White;
        }

        private Color AccentColor(string name)
        {
            string raw = name ?? "purple";
            if (raw.StartsWith("#", StringComparison.Ordinal) && raw.Length == 7)
            {
                int rgb;
                if (Int32.TryParse(raw.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb))
                    return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
            }
            switch (raw.ToLowerInvariant())
            {
                case "blue": return Color.FromArgb(64, 131, 255);
                case "cyan": return Color.FromArgb(0, 190, 210);
                case "green": return Color.FromArgb(46, 190, 118);
                case "orange": return Color.FromArgb(238, 144, 55);
                case "red": return Color.FromArgb(222, 72, 82);
                default: return Color.FromArgb(124, 36, 230);
            }
        }

        private void ApplyLanguage()
        {
            _navPower.Text = T("navPower"); _navTune.Text = T("navTune"); _navTelemetry.Text = T("navTelemetry"); _navCompat.Text = T("navCompat"); _navSettings.Text = T("navSettings"); _resetAll.Text = T("resetAll");
            SetNamedText(this);
            _workflowText.Text = T("workflowBody");
            _settingsNote.Text = T("settingsNote");
            _rebootNow.Text = T("rebootNow");
            if (_telemetryTogglePage != null) _telemetryTogglePage.Text = T("telemetryPageToggle");
            if (_telemetryRefreshBtn != null) _telemetryRefreshBtn.Text = T("telemetryManualRefresh");
            if (_telemetryStatusLabel != null)
            {
                if (!_settings.TelemetryEnabled)
                    _telemetryStatusLabel.Text = T("telemetryStatusOff");
                else
                    _telemetryStatusLabel.Text = _settings.Language == "ru"
                        ? ("Автообновление активно (" + _settings.TelemetryIntervalMs + " мс)")
                        : ("Live polling active (" + _settings.TelemetryIntervalMs + " ms)");
            }
            UpdateExperimentalLabel();
        }

        private void SetNamedText(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (!String.IsNullOrEmpty(c.Name) && Tr.ContainsKey(c.Name)) c.Text = T(c.Name);
                SetNamedText(c);
            }
        }

        private string T(string key)
        {
            string[] v;
            if (!Tr.TryGetValue(key, out v)) return key;
            return _settings.Language == "en" ? v[1] : v[0];
        }

        private static Dictionary<string, string[]> BuildTranslations()
        {
            Dictionary<string, string[]> d = new Dictionary<string, string[]>();
            d["navPower"] = new string[] { "Питание", "Power" };
            d["navTune"] = new string[] { "Разгон", "Tuning" };
            d["navTelemetry"] = new string[] { "Телеметрия", "Telemetry" };
            d["navCompat"] = new string[] { "Совместимость", "Compatibility" };
            d["navSettings"] = new string[] { "Настройки", "Settings" };
            d["resetAll"] = new string[] { "Сбросить всё", "Reset all" };
            d["powerSection"] = new string[] { "БЫСТРАЯ НАСТРОЙКА ПИТАНИЯ", "QUICK POWER TUNING" };
            d["powerSectionSub"] = new string[] { "MAX выбирается шкалой и сохраняется через VBIOS shadow; CURRENT применяется в runtime. Resolver сверяет GPU, PCI, VBIOS и live policy перед записью.", "Choose MAX with the slider and store it through the VBIOS shadow; CURRENT is runtime. The resolver cross-checks GPU, PCI, VBIOS and live policy before writes." };
            d["maxTitle"] = new string[] { "MAX Power Limit", "MAX Power Limit" };
            d["currentTitle"] = new string[] { "CURRENT Power Limit", "CURRENT Power Limit" };
            d["maxLiveLabel"] = new string[] { "Текущий MAX", "Live MAX" };
            d["currentLiveLabel"] = new string[] { "Текущий CURRENT", "Live CURRENT" };
            d["target"] = new string[] { "Целевое значение", "Target" };
            d["targetPower"] = new string[] { "Выбранный лимит", "Selected limit" };
            d["applyMax"] = new string[] { "Сохранить MAX", "Save MAX" };
            d["applyCurrent"] = new string[] { "Применить CURRENT", "Apply CURRENT" };
            d["restoreCurrent"] = new string[] { "CURRENT заводской", "Factory CURRENT" };
            d["restoreMax"] = new string[] { "MAX заводской", "Factory MAX" };
            d["msiAutoApply"] = new string[] { "Авто-применять CURRENT при смене режима MSI Center (kernel event, 0% CPU)", "Auto-reapply CURRENT on MSI Center mode change (kernel event, 0% CPU)" };
            d["msiTimeoutLabel"] = new string[] { "Задержка применения MSI (1..10 с)", "MSI Center re-apply delay (1..10s)" };
            d["closeToTray"] = new string[] { "Сворачивать в системный трей при закрытии (0% CPU)", "Minimize to system tray on close (0% CPU background watcher)" };
            d["trayOpen"] = new string[] { "Открыть NvpwrControl", "Open NvpwrControl" };
            d["trayReapply"] = new string[] { "Применить настройки сейчас", "Re-apply Settings Now" };
            d["trayExit"] = new string[] { "Выход", "Exit NvpwrControl" };
            d["telemetryPageToggle"] = new string[] { "Включить автоопрос телеметрии (опрос GPU)", "Enable Live Telemetry Polling (GPU polling)" };
            d["telemetryManualRefresh"] = new string[] { "Обновить вручную", "Refresh Once" };
            d["telemetryStatusOff"] = new string[] { "Опрос отключен (0% фоновой нагрузки)", "Polling disabled (0% background overhead)" };
            d["voltTitle"] = new string[] { "Целевое напряжение (NVVDD)", "Core Voltage Target (NVVDD)" };
            d["voltLiveLabel"] = new string[] { "Текущее напряжение", "Live Voltage" };
            d["targetVoltage"] = new string[] { "Целевое напряжение", "Target voltage" };
            d["applyVolt"] = new string[] { "Применить напряжение", "Apply Voltage" };
            d["restartDriver"] = new string[] { "Перезапустить драйвер", "Restart Driver" };
            d["restoreVolt"] = new string[] { "Заводское (940 мВ)", "Factory (940 mV)" };
            d["voltInfoTitle"] = new string[] { "Архитектура напряжения", "Voltage Architecture" };
            d["voltInfoDesc"] = new string[] {
                "Мобильные GPU серии RTX 40/50 имеют заводское ограничение надежности VRel на уровне 940 мВ в микрокоде PMU.\r\n\r\n" +
                "Данный инструмент выполняет:\r\n" +
                "1. Программирование кривой V/F через прямой вызов NvAPI ClkVfPointsSetControl (0x0733E009).\r\n" +
                "2. Установку RmPerfLimitsOverride = 0x50 в реестре драйвера для снятия лимитов VRel и VOp в nvlddmkm.sys.\r\n\r\n" +
                "Для применения требуются права Администратора.",
                "Mobile Ada and Blackwell GPUs have a stock reliability clamp (VRel) at 940 mV enforced in PMU microcode.\r\n\r\n" +
                "This control performs:\r\n" +
                "1. Real-time V/F curve point programming via NvAPI ClkVfPointsSetControl (0x0733E009).\r\n" +
                "2. Driver arbiter limit bypass via RmPerfLimitsOverride = 0x50 and RMEnableOverclockingAllPstates = 1 in nvlddmkm.sys registry.\r\n\r\n" +
                "Administrator privileges are required to write the V/F curve and driver settings."
            };
            d["startupTitle"] = new string[] { "Автоприменение CURRENT", "CURRENT autostart" };
            d["startupDesc"] = new string[] { "После входа в Windows программа дождётся NVIDIA, проверит MAX и применит выбранный CURRENT от администратора.", "After Windows logon the app waits for NVIDIA, checks MAX and applies the selected CURRENT elevated." };
            d["saveAutostart"] = new string[] { "Сохранить автозапуск", "Save autostart" };
            d["removeAutostart"] = new string[] { "Убрать автозапуск", "Remove autostart" };
            d["workflowTitle"] = new string[] { "Правильный порядок", "Correct workflow" };
            d["workflowBody"] = new string[] { "1. Выберите MAX и сохраните.\r\n2. Перезагрузите Windows.\r\n3. Снова откройте программу и убедитесь, что MAX изменился.\r\n4. Только после этого применяйте CURRENT.\r\n5. CURRENT меняйте на простое GPU, до запуска нагрузки.", "1. Choose and save MAX.\r\n2. Reboot Windows.\r\n3. Reopen the app and verify MAX changed.\r\n4. Only then apply CURRENT.\r\n5. Change CURRENT while the GPU is idle, before starting a workload." };
            d["profileRange"] = new string[] { "Диапазон профиля", "Profile range" };
            d["overrideNone"] = new string[] { "romOverride: отсутствует", "romOverride: none" };
            d["overrideUnknown"] = new string[] { "romOverride: неизвестное значение", "romOverride: unknown value" };
            d["referenceValidated"] = new string[] { "Уровень входит в проверенный диапазон эталонной системы.", "Target is within the reference-validated range." };
            d["experimentalTarget"] = new string[] { "Экспериментальный уровень для этого SKU/VBIOS — требуется собственная проверка стабильности.", "Experimental for this SKU/VBIOS — validate stability on the actual laptop." };
            d["rebootApplyMax"] = new string[] { "MAX {W} W записан. Нужна перезагрузка. После запуска Windows снова откройте программу, проверьте MAX и затем выставьте CURRENT.", "MAX {W} W is saved. Reboot is required. After Windows starts, reopen the app, verify MAX, then set CURRENT." };
            d["rebootRestoreMax"] = new string[] { "MAX override удалён. Нужна перезагрузка для возврата заводского MAX {W} W.", "MAX override was removed. Reboot to restore factory MAX {W} W." };
            d["rebootNow"] = new string[] { "Перезагрузить сейчас", "Reboot now" };
            d["tuningSection"] = new string[] { "РАЗГОН GPU", "GPU TUNING" };
            d["tuningSectionSub"] = new string[] { "Каждый блок включается только после live GET/валидации структуры и проверяется повторным GET после SET.", "Each control is enabled only after live GET/layout validation and is verified by GET after SET." };
            d["coreTitle"] = new string[] { "Частота ядра (Core)", "Core clock" };
            d["coreTarget"] = new string[] { "Смещение частоты ядра", "Core clock offset" };
            d["memoryTitle"] = new string[] { "Частота памяти (Memory)", "Memory clock" };
            d["memoryTarget"] = new string[] { "Смещение частоты памяти", "Memory clock offset" };
            d["xbarTitle"] = new string[] { "Частота XBAR", "XBAR clock" };
            d["xbarTarget"] = new string[] { "Смещение частоты XBAR", "XBAR clock offset" };
            d["msvddTitle"] = new string[] { "Напряжение MSVDD", "MSVDD voltage" };
            d["msvddTarget"] = new string[] { "Смещение MSVDD", "MSVDD offset" };
            d["nvvddTitle"] = new string[] { "Напряжение NVVDD", "NVVDD voltage" };
            d["nvvddTarget"] = new string[] { "Смещение NVVDD", "NVVDD offset" };
            d["ratioTitle"] = new string[] { "Коэффициент GPC:XBAR", "GPC:XBAR ratio" };
            d["ratioTarget"] = new string[] { "Целевой коэффициент", "Target ratio" };
            d["enabledToggle"] = new string[] { "Использовать", "Enabled" };
            d["probeTune"] = new string[] { "Обновить возможности", "Refresh capabilities" };
            d["applyTune"] = new string[] { "Применить выбранное", "Apply selected" };
            d["resetTune"] = new string[] { "Сбросить разгон", "Reset tuning" };
            d["applied"] = new string[] { "Применено", "Applied" };
            d["allowed"] = new string[] { "Допустимо", "Allowed" };
            d["telemetrySection"] = new string[] { "ТЕЛЕМЕТРИЯ", "TELEMETRY" };
            d["telemetrySectionSub"] = new string[] { "Текущие датчики NVIDIA/NVML. Недоступные датчики показываются как N/A.", "Live NVIDIA/NVML sensors. Unavailable sensors are shown as N/A." };
            d["metricPower"] = new string[] { "Мощность", "Power" }; d["metricTemp"] = new string[] { "Температура GPU", "GPU temperature" }; d["metricHotspot"] = new string[] { "Hotspot", "Hotspot" }; d["metricMemTemp"] = new string[] { "Температура памяти", "Memory temperature" }; d["metricUtil"] = new string[] { "Загрузка GPU", "GPU utilization" }; d["metricCore"] = new string[] { "Частота ядра", "Core clock" }; d["metricMem"] = new string[] { "Частота памяти", "Memory clock" }; d["metricCurrent"] = new string[] { "CURRENT", "CURRENT" }; d["metricMax"] = new string[] { "MAX", "MAX" };
            d["restartNv"] = new string[] { "Перезапустить NVIDIA device", "Restart NVIDIA device" };
            d["compatSection"] = new string[] { "РЕСОЛЬВЕРЫ И СОВМЕСТИМОСТЬ", "RESOLVERS & COMPATIBILITY" };
            d["compatSectionSub"] = new string[] { "Неизвестный VBIOS автоматически проходит read-only ROM resolver; новый драйвер — pattern + semantic no-op validation. При неоднозначности запись блокируется.", "Unknown VBIOSes automatically run the read-only ROM resolver; new drivers use pattern + semantic no-op validation. Ambiguous results keep writes locked." };
            d["compatActionsTitle"] = new string[] { "Проверка и резольв", "Validation & resolver" };
            d["compatReportTitle"] = new string[] { "Диагностический отчёт", "Diagnostic report" };
            d["resolverGpu"] = new string[] { "GPU / PCI ПРОФИЛЬ", "GPU / PCI PROFILE" };
            d["resolverDriver"] = new string[] { "ДРАЙВЕР / RM", "DRIVER / RM" };
            d["resolverVbios"] = new string[] { "VBIOS / MAX", "VBIOS / MAX" };
            d["resolverPolicy"] = new string[] { "СОСТОЯНИЕ ПОЛИТИКИ", "POLICY STATE" };
            d["validateDriver"] = new string[] { "Проверить новый драйвер", "Validate new driver" };
            d["autoVbios"] = new string[] { "Авто-резолв VBIOS", "Auto-resolve VBIOS" };
            d["selectRom"] = new string[] { "Выбрать ROM", "Select ROM" };
            d["recheck"] = new string[] { "Перепроверить", "Recheck" };
            d["exportReport"] = new string[] { "Экспорт отчёта", "Export report" };
            d["openLog"] = new string[] { "Открыть лог", "Open log" };
            d["settingsSection"] = new string[] { "НАСТРОЙКИ ИНТЕРФЕЙСА", "INTERFACE SETTINGS" };
            d["settingsSectionSub"] = new string[] { "Тема, общий акцент, цвета кнопок/шкал и масштаб сохраняются для текущего пользователя.", "Theme, global accent, button/slider colors and scale are stored for the current user." };
            d["appearanceTitle"] = new string[] { "Внешний вид", "Appearance" }; d["behaviorTitle"] = new string[] { "Поведение", "Behavior" };
            d["language"] = new string[] { "Язык", "Language" }; d["theme"] = new string[] { "Тема", "Theme" }; d["accent"] = new string[] { "Общий акцент", "Global accent" };
            d["buttonAccent"] = new string[] { "Цвет основных кнопок", "Primary button color" };
            d["sliderAccent"] = new string[] { "Цвет шкал", "Slider color" };
            d["uiScale"] = new string[] { "Масштаб UI", "UI scale" }; d["telemetryInterval"] = new string[] { "Интервал телеметрии", "Telemetry interval" };
            d["telemetryEnabled"] = new string[] { "Автоматически обновлять телеметрию", "Automatically refresh telemetry" };
            d["settingsNote"] = new string[] { "Per-Monitor DPI V2 включён. Изменение пользовательского масштаба применяется после перезапуска программы.", "Per-Monitor DPI V2 is enabled. User scale changes apply after restarting the app." };
            d["scaleRestart"] = new string[] { "Новый масштаб сохранён. Перезапустите программу, чтобы применить его полностью.", "New scale saved. Restart the app to apply it fully." };
            d["ready"] = new string[] { "POWER READY", "POWER READY" }; d["vbiosNeeded"] = new string[] { "НУЖЕН РЕЗОЛЬВ VBIOS", "VBIOS RESOLVE NEEDED" }; d["needsValidation"] = new string[] { "НУЖНА ПРОВЕРКА ДРАЙВЕРА", "DRIVER VALIDATION NEEDED" }; d["limited"] = new string[] { "ОГРАНИЧЕННЫЙ РЕЖИМ", "LIMITED MODE" }; d["unsupportedGpu"] = new string[] { "НЕПОДДЕРЖИВАЕМЫЙ GPU", "UNSUPPORTED GPU" };
            d["validationRequired"] = new string[] { "требуется проверка", "validation required" };
            d["policyLocked"] = new string[] { "ПОЛИТИКА НЕСОГЛАСОВАНА — ЗАПИСЬ ЗАБЛОКИРОВАНА", "INCONSISTENT POLICY — WRITES LOCKED" };
            d["checking"] = new string[] { "Проверка GPU / драйвера / VBIOS…", "Checking GPU / driver / VBIOS…" }; d["resolvingVbios"] = new string[] { "Резолв VBIOS…", "Resolving VBIOS…" };
            d["resolveVbiosFirst"] = new string[] { "MAX заблокирован: сначала откройте Совместимость и выполните VBIOS resolver. Для неизвестного VBIOS программа может использовать nvflash read-only dump или выбранный ROM-файл.", "MAX is locked: open Compatibility and resolve the VBIOS first. For an unknown VBIOS the app can use an nvflash read-only dump or a selected ROM file." };
            d["validateDriverFirst"] = new string[] { "CURRENT заблокирован: новый драйвер найден, но его RM transport ещё не прошёл semantic no-op проверку. Откройте Совместимость → Проверить новый драйвер.", "CURRENT is locked: a new driver candidate was found but its RM transport has not passed semantic no-op validation. Open Compatibility → Validate new driver." };
            d["selectRomTitle"] = new string[] { "Выберите дамп VBIOS", "Select a VBIOS dump" };
            d["compatSafetyText"] = new string[] { "Resolver работает fail-closed: точный GPU/PCI профиль + единственный совместимый VBIOS Power Budget + проверенный RM transport + согласованный CURRENT/MAX state. После SET выполняется readback; при ошибке CURRENT/MAX transaction откатывается, если это возможно.", "The resolver is fail-closed: exact GPU/PCI profile + one compatible VBIOS Power Budget + validated RM transport + coherent CURRENT/MAX state. Every SET is read back; CURRENT/MAX transactions roll back when possible on failure." };
            d["experimentalConfirm"] = new string[] { "{W} W для {GPU} не входит в физически проверенный эталонный диапазон этой сборки. Resolver подтверждает только путь управления, а не электрическую/тепловую устойчивость конкретного ноутбука. Продолжить?", "{W} W on {GPU} is outside this build's physically reference-validated range. The resolver validates the control path, not electrical/thermal stability of this specific laptop. Continue?" };
            d["factoryConfirm"] = new string[] { "Сбросить разгон, вернуть CURRENT к stock, убрать автозапуск и удалить tool-owned MAX override? Для возврата MAX потребуется перезагрузка.", "Reset tuning, restore CURRENT to stock, remove autostart and delete the tool-owned MAX override? Restoring MAX requires a reboot." };
            d["restartNvWarn"] = new string[] { "Перезапуск NVIDIA device может закрыть приложения, использующие GPU. Продолжить?", "Restarting the NVIDIA device can close applications using the GPU. Continue?" };
            d["rebootConfirm"] = new string[] { "Сейчас перезагрузить Windows? Сохраните работу в других приложениях.", "Reboot Windows now? Save work in other applications first." };
            return d;
        }

        private void ShowResult(OperationResult r)
        {
            string msg = r.Message;
            if (r.RebootRequired) msg += "\r\n\r\n" + T("workflowBody");
            MessageBox.Show(this, msg, r.Success ? "NvpwrControl" : "NvpwrControl — error", MessageBoxButtons.OK, r.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }

        private string W(double? v) { return v.HasValue ? v.Value.ToString("0.00", CultureInfo.InvariantCulture) + " W" : "N/A"; }
        private string D(double? v, string unit) { return v.HasValue ? v.Value.ToString("0.##", CultureInfo.InvariantCulture) + unit : "N/A"; }
        private void SetNumeric(NumericUpDown n, decimal value) { if (value < n.Minimum) value = n.Minimum; if (value > n.Maximum) value = n.Maximum; n.Value = value; }
        private void SetStatus(string s) { if (_statusText != null) _statusText.Text = s; }

        private void ExportReport()
        {
            SaveFileDialog d = new SaveFileDialog { Filter = "Text file|*.txt", FileName = "NvpwrCompatibility-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt" };
            if (d.ShowDialog(this) == DialogResult.OK) ShowResult(_power.ExportCompatibilityReport(d.FileName));
        }

        private void SaveSettings()
        {
            try
            {
                if (WindowState == FormWindowState.Normal)
                {
                    _settings.WindowWidth = Width;
                    _settings.WindowHeight = Height;
                    _settings.WindowMaximized = false;
                }
                else if (WindowState == FormWindowState.Maximized)
                {
                    _settings.WindowMaximized = true;
                }
                SettingsStore.Save(_settings);
            }
            catch { }
        }
    }
}
