using System;
using System.Globalization;
using NvpwrControlBlackwell;

namespace MsiAfterburnerSync
{
    public static class PowerUnlockController
    {
        private static readonly object _syncLock = new object();

        public static double? GetCurrentPowerLimit()
        {
            try
            {
                PowerBackend backend = new PowerBackend();
                PowerState ps = backend.GetPowerState();
                return ps.CurrentW;
            }
            catch
            {
                return null;
            }
        }

        public static bool ApplyPowerLimit(int targetWatts, out string message)
        {
            lock (_syncLock)
            {
                try
                {
                    PowerBackend backend = new PowerBackend();
                    CompatibilityState c = backend.CheckCompatibility();

                    if (c.Profile == null)
                    {
                        message = "Unsupported GPU profile (" + (c.Reason ?? "unknown") + ").";
                        return false;
                    }

                    if (!c.CurrentWritesReady)
                    {
                        if (c.Driver != null && c.Driver.CandidateFound && !c.Driver.Trusted)
                        {
                            try
                            {
                                backend.ValidateDriverResolver();
                                c = backend.CheckCompatibility();
                            }
                            catch { }
                        }
                    }

                    if (!c.CurrentWritesReady)
                    {
                        message = "Driver transport not validated/trusted: " + (c.Driver != null ? c.Driver.Reason : c.Reason);
                        return false;
                    }

                    if (!c.Profile.IsInRange(targetWatts))
                    {
                        message = "Target " + targetWatts + "W outside supported profile range (" + c.Profile.MinPowerW + "-" + c.Profile.MaxPowerW + "W).";
                        return false;
                    }

                    PowerState ps = backend.GetPowerState();
                    if (!ps.MaxW.HasValue)
                    {
                        message = "Public NVIDIA Max Power Limit is unavailable.";
                        return false;
                    }

                    if (targetWatts > (int)Math.Floor(ps.MaxW.Value + 0.01))
                    {
                        message = "Target " + targetWatts + "W exceeds live MAX limit (" + ps.MaxW.Value.ToString("0.0", CultureInfo.InvariantCulture) + "W).";
                        return false;
                    }

                    if (ps.CurrentW.HasValue && Math.Abs(ps.CurrentW.Value - targetWatts) <= 1.0)
                    {
                        message = "Power limit already at " + targetWatts + "W.";
                        return true;
                    }

                    OperationResult result = backend.SetCurrent(targetWatts);
                    message = result.Message;
                    return result.Success;
                }
                catch (Exception ex)
                {
                    message = "Power apply exception: " + ex.Message;
                    return false;
                }
            }
        }
    }
}
