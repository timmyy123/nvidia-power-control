using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace NvpwrControlBlackwell
{
    internal sealed class MsiScenarioWatcher : IDisposable
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

        private readonly Action _onScenarioChanged;
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
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public MsiScenarioWatcher(Action onScenarioChanged)
        {
            _onScenarioChanged = onScenarioChanged;
        }

        public void Start()
        {
            if (_workerThread != null) return;
            if (!IsMsiCenterInstalled()) return;

            _stopEvent = new ManualResetEvent(false);
            _workerThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "MsiScenarioWatcher",
                Priority = ThreadPriority.BelowNormal
            };
            _workerThread.Start();
            AppLog.Write("MsiScenarioWatcher started: listening for MSI Center scenario kernel events (0% CPU).");
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
            AppLog.Write("MsiScenarioWatcher stopped.");
        }

        private void ListenLoop()
        {
            AutoResetEvent changeEvent = new AutoResetEvent(false);
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (RegistryKey key = baseKey.OpenSubKey(MsiBaseModulePath, false))
                {
                    if (key == null) return;
                    IntPtr hKey = key.Handle.DangerousGetHandle();
                    WaitHandle[] waitHandles = new WaitHandle[] { _stopEvent, changeEvent };

                    while (!_stopEvent.WaitOne(0))
                    {
                        int res = RegNotifyChangeKeyValue(
                            hKey,
                            true,
                            REG_NOTIFY_CHANGE_LAST_SET,
                            changeEvent.SafeWaitHandle.DangerousGetHandle(),
                            true);

                        if (res != 0)
                        {
                            AppLog.Write("MsiScenarioWatcher: RegNotifyChangeKeyValue returned " + res);
                            break;
                        }

                        int signaled = WaitHandle.WaitAny(waitHandles);
                        if (signaled == 0) // Stop event was signaled
                            break;

                        // Change event was signaled!
                        // Wait user-configured timeout (1 to 10 seconds) for MSI Center to finish its mode reconfiguration
                        int waitMs = Math.Max(1000, Math.Min(10000, TimeoutSec * 1000));
                        if (_stopEvent.WaitOne(waitMs)) break;

                        // Drain any extra signals queued during the 1s wait so we only apply ONCE
                        changeEvent.Reset();

                        try
                        {
                            if (_onScenarioChanged != null)
                                _onScenarioChanged();
                        }
                        catch (Exception ex)
                        {
                            AppLog.Write("MsiScenarioWatcher callback error: " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("MsiScenarioWatcher thread exited: " + ex.Message);
            }
            finally
            {
                try { changeEvent.Dispose(); } catch { }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            if (_stopEvent != null)
            {
                try { _stopEvent.Dispose(); } catch { }
                _stopEvent = null;
            }
        }
    }
}
