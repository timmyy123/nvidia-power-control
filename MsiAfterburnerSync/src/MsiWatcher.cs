using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace MsiAfterburnerSync
{
    public sealed class MsiWatcher : IDisposable
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegNotifyChangeKeyValue(
            IntPtr hKey,
            bool bWatchSubtree,
            uint dwNotifyFilter,
            IntPtr hEvent,
            bool fAsynchronous);

        private const uint REG_NOTIFY_CHANGE_LAST_SET = 4;
        private const string MsiBaseModulePath = @"SOFTWARE\MSI\MSI Center\Component\Base Module";
        private const string MsiUserScenarioPath = @"SOFTWARE\MSI\MSI Center\Component\Base Module\User Scenario";

        private readonly Action _onScenarioChanged;
        private readonly Action<string> _logger;
        public volatile int TimeoutSec = 1;
        private Thread _workerThread;
        private ManualResetEvent _stopEvent;
        private bool _disposed;

        public static bool IsMsiCenterInstalled()
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (RegistryKey key = baseKey.OpenSubKey(MsiBaseModulePath, false))
                {
                    if (key != null) return true;
                }
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = baseKey.OpenSubKey(MsiBaseModulePath, false))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public MsiWatcher(Action onScenarioChanged, Action<string> logger = null)
        {
            _onScenarioChanged = onScenarioChanged;
            _logger = logger;
        }

        private void Log(string msg)
        {
            if (_logger != null)
            {
                try { _logger(msg); } catch { }
            }
        }

        public void Start()
        {
            if (_workerThread != null) return;

            _stopEvent = new ManualResetEvent(false);
            _workerThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "MsiWatcherThread",
                Priority = ThreadPriority.BelowNormal
            };
            _workerThread.Start();
        }

        public void Stop()
        {
            if (_stopEvent != null)
            {
                try { _stopEvent.Set(); } catch { }
            }
            if (_workerThread != null)
            {
                try { _workerThread.Join(500); } catch { }
                _workerThread = null;
            }
        }

        private static int ReadCurrentScenarioMode()
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (RegistryKey key = baseKey.OpenSubKey(MsiUserScenarioPath, false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("Mode");
                        if (val != null) return Convert.ToInt32(val);
                    }
                }
            }
            catch { }
            return -1;
        }

        private void ListenLoop()
        {
            AutoResetEvent changeEvent = new AutoResetEvent(false);
            RegistryKey targetKey = null;

            try
            {
                // Try User Scenario first, fallback to Base Module
                try
                {
                    RegistryKey baseKey32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                    targetKey = baseKey32.OpenSubKey(MsiUserScenarioPath, false) ?? baseKey32.OpenSubKey(MsiBaseModulePath, false);
                }
                catch { }

                if (targetKey == null)
                {
                    try
                    {
                        RegistryKey baseKey64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                        targetKey = baseKey64.OpenSubKey(MsiUserScenarioPath, false) ?? baseKey64.OpenSubKey(MsiBaseModulePath, false);
                    }
                    catch { }
                }

                if (targetKey == null)
                {
                    Log("MsiWatcher: Neither Base Module nor User Scenario registry key could be opened.");
                }
                else
                {
                    Log("MsiWatcher: Successfully hooked registry key for event notifications (0% CPU).");
                }

                int lastMode = ReadCurrentScenarioMode();
                WaitHandle[] waitHandles = new WaitHandle[] { _stopEvent, changeEvent };

                while (!_stopEvent.WaitOne(0))
                {
                    IntPtr hKey = targetKey != null ? targetKey.Handle.DangerousGetHandle() : IntPtr.Zero;
                    if (hKey != IntPtr.Zero)
                    {
                        int res = RegNotifyChangeKeyValue(
                            hKey,
                            true,
                            REG_NOTIFY_CHANGE_LAST_SET,
                            changeEvent.SafeWaitHandle.DangerousGetHandle(),
                            true);

                        if (res != 0)
                        {
                            Log("MsiWatcher: RegNotifyChangeKeyValue returned code " + res + ". Falling back to 250ms polling.");
                            hKey = IntPtr.Zero;
                        }
                    }

                    if (hKey != IntPtr.Zero)
                    {
                        // Wait up to 500ms or until registry event signals
                        int signaled = WaitHandle.WaitAny(waitHandles, 500);
                        if (signaled == 0) break; // StopEvent signaled
                        
                        // Check if Mode value changed or if event was signaled
                        int currentMode = ReadCurrentScenarioMode();
                        bool changed = (signaled == 1) || (currentMode != lastMode && currentMode >= 0 && lastMode >= 0);
                        if (!changed)
                        {
                            continue;
                        }

                        Log("MsiWatcher: Scenario switch detected (Mode " + lastMode + " -> " + currentMode + ")! Waiting " + TimeoutSec + "s...");
                        lastMode = currentMode;

                        // Wait user configured delay (1-10s)
                        int waitMs = Math.Max(1000, Math.Min(10000, TimeoutSec * 1000));
                        if (_stopEvent.WaitOne(waitMs)) break;

                        changeEvent.Reset();

                        try
                        {
                            if (_onScenarioChanged != null) _onScenarioChanged();
                        }
                        catch (Exception ex)
                        {
                            Log("MsiWatcher callback exception: " + ex.Message);
                        }
                    }
                    else
                    {
                        // Safety fallback polling (250ms interval, 0% CPU) if registry handle notifications are blocked
                        if (_stopEvent.WaitOne(250)) break;

                        int currentMode = ReadCurrentScenarioMode();
                        if (currentMode >= 0 && lastMode >= 0 && currentMode != lastMode)
                        {
                            Log("MsiWatcher: Polled scenario switch (Mode " + lastMode + " -> " + currentMode + ")! Waiting " + TimeoutSec + "s...");
                            lastMode = currentMode;

                            int waitMs = Math.Max(1000, Math.Min(10000, TimeoutSec * 1000));
                            if (_stopEvent.WaitOne(waitMs)) break;

                            try
                            {
                                if (_onScenarioChanged != null) _onScenarioChanged();
                            }
                            catch (Exception ex)
                            {
                                Log("MsiWatcher callback exception: " + ex.Message);
                            }
                        }
                        else if (lastMode < 0 && currentMode >= 0)
                        {
                            lastMode = currentMode;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("MsiWatcher thread encountered exception: " + ex.Message);
            }
            finally
            {
                if (targetKey != null)
                {
                    try { targetKey.Close(); } catch { }
                }
                changeEvent.Close();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            if (_stopEvent != null)
            {
                _stopEvent.Close();
                _stopEvent = null;
            }
        }
    }
}
