using System;
using System.Collections.Generic;
using System.Globalization;

namespace NvpwrControlBlackwell
{
    internal sealed class GpuProfile
    {
        public string Id = "";
        public string Match = "";
        public string[] Aliases = null;
        public string DisplayName = "";
        public int MinPowerW;
        public int StockPowerW;
        public int MaxPowerW;
        public int ReferenceValidatedW;
        public bool AllowMsvdd;
        public int[] KnownDeviceIds = new int[0];
        public int[] AlternativeStockWatts = null;

        public int MinW
        {
            get { return MinPowerW > 0 ? MinPowerW : StockPowerW; }
        }

        public int[] Targets()
        {
            List<int> values = new List<int>();
            for (int w = MinW; w <= MaxPowerW; w += 5) values.Add(w);
            if (values.Count == 0 || values[values.Count - 1] != MaxPowerW) values.Add(MaxPowerW);
            return values.ToArray();
        }

        public bool IsInRange(int watts)
        {
            return watts >= MinW && watts <= MaxPowerW && ((watts - MinW) % 5 == 0 || watts == MaxPowerW);
        }

        public bool IsReferenceValidated(int watts)
        {
            return watts <= ReferenceValidatedW;
        }

        public bool DeviceIdMatches(int id)
        {
            if (id <= 0 || KnownDeviceIds == null || KnownDeviceIds.Length == 0) return false;
            foreach (int x in KnownDeviceIds) if (x == id) return true;
            return false;
        }

        public bool MatchesName(string gpuName)
        {
            if (String.IsNullOrEmpty(gpuName)) return false;
            if (!String.IsNullOrEmpty(Match) && gpuName.IndexOf(Match, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (Aliases != null)
            {
                foreach (string a in Aliases)
                {
                    if (!String.IsNullOrEmpty(a) && gpuName.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            return false;
        }

        public override string ToString()
        {
            return DisplayName + "  " + MinW.ToString() + "–" + MaxPowerW.ToString() + " W";
        }
    }

    internal static class GpuProfiles
    {
        private static readonly GpuProfile[] Profiles = new GpuProfile[]
        {
            // RTX 50 Series Laptop GPUs (Blackwell)
            new GpuProfile { Id="5050", Match="RTX 5050 Laptop", Aliases=new string[] { "RTX 5050 Mobile" }, DisplayName="RTX 5050 Laptop", MinPowerW=115, StockPowerW=115, MaxPowerW=140, ReferenceValidatedW=115, AllowMsvdd=false },
            new GpuProfile { Id="5060", Match="RTX 5060 Laptop", Aliases=new string[] { "RTX 5060 Mobile" }, DisplayName="RTX 5060 Laptop", MinPowerW=115, StockPowerW=115, MaxPowerW=140, ReferenceValidatedW=115, AllowMsvdd=false },
            new GpuProfile { Id="5070ti", Match="RTX 5070 Ti Laptop", Aliases=new string[] { "RTX 5070 Ti Mobile", "RTX 5070Ti Laptop", "RTX 5070Ti Mobile" }, DisplayName="RTX 5070 Ti Laptop", MinPowerW=140, StockPowerW=140, MaxPowerW=180, ReferenceValidatedW=160, AllowMsvdd=true, KnownDeviceIds=new int[] { 0x2F58 } },
            new GpuProfile { Id="5070", Match="RTX 5070 Laptop", Aliases=new string[] { "RTX 5070 Mobile" }, DisplayName="RTX 5070 Laptop", MinPowerW=115, StockPowerW=115, MaxPowerW=140, ReferenceValidatedW=115, AllowMsvdd=false },
            new GpuProfile { Id="5080", Match="RTX 5080 Laptop", Aliases=new string[] { "RTX 5080 Mobile" }, DisplayName="RTX 5080 Laptop", MinPowerW=175, StockPowerW=175, MaxPowerW=250, ReferenceValidatedW=175, AllowMsvdd=true },
            new GpuProfile { Id="5090", Match="RTX 5090 Laptop", Aliases=new string[] { "RTX 5090 Mobile" }, DisplayName="RTX 5090 Laptop", MinPowerW=175, StockPowerW=175, MaxPowerW=250, ReferenceValidatedW=175, AllowMsvdd=true },

            // RTX 40 Series Laptop GPUs (Ada Lovelace)
            new GpuProfile { Id="4090", Match="RTX 4090 Laptop", Aliases=new string[] { "RTX 4090 Mobile" }, DisplayName="RTX 4090 Laptop", MinPowerW=150, StockPowerW=175, MaxPowerW=250, ReferenceValidatedW=175, AllowMsvdd=false, KnownDeviceIds=new int[] { 0x2717, 0x2757 }, AlternativeStockWatts=new int[] { 150 } },
            new GpuProfile { Id="4080", Match="RTX 4080 Laptop", Aliases=new string[] { "RTX 4080 Mobile" }, DisplayName="RTX 4080 Laptop", MinPowerW=150, StockPowerW=175, MaxPowerW=225, ReferenceValidatedW=175, AllowMsvdd=false, KnownDeviceIds=new int[] { 0x27A0, 0x27E0 }, AlternativeStockWatts=new int[] { 150 } },
            new GpuProfile { Id="4070", Match="RTX 4070 Laptop", Aliases=new string[] { "RTX 4070 Mobile" }, DisplayName="RTX 4070 Laptop", MinPowerW=120, StockPowerW=140, MaxPowerW=150, ReferenceValidatedW=140, AllowMsvdd=false, KnownDeviceIds=new int[] { 0x2820, 0x2860 }, AlternativeStockWatts=new int[] { 115, 120 } },
            new GpuProfile { Id="4060", Match="RTX 4060 Laptop", Aliases=new string[] { "RTX 4060 Mobile" }, DisplayName="RTX 4060 Laptop", MinPowerW=120, StockPowerW=140, MaxPowerW=150, ReferenceValidatedW=140, AllowMsvdd=false, KnownDeviceIds=new int[] { 0x2882, 0x28A0, 0x28E0 }, AlternativeStockWatts=new int[] { 115, 120 } },
            new GpuProfile { Id="4050", Match="RTX 4050 Laptop", Aliases=new string[] { "RTX 4050 Mobile" }, DisplayName="RTX 4050 Laptop", MinPowerW=115, StockPowerW=115, MaxPowerW=140, ReferenceValidatedW=115, AllowMsvdd=false, KnownDeviceIds=new int[] { 0x28A1, 0x28E1 }, AlternativeStockWatts=new int[] { 95, 105 } }
        };

        public static GpuProfile Detect(string gpuName)
        {
            return Detect(gpuName, null);
        }

        public static GpuProfile Detect(string gpuName, string pnpId)
        {
            if (String.IsNullOrEmpty(gpuName)) return null;
            int dev = ParseHexToken(pnpId, "DEV_", 4);

            // Prefer a profile whose known PCI device ID agrees with Windows.
            foreach (GpuProfile p in Profiles)
            {
                if (p.MatchesName(gpuName) && p.DeviceIdMatches(dev))
                    return p;
            }

            foreach (GpuProfile p in Profiles)
                if (p.MatchesName(gpuName)) return p;

            // Also check device ID match if name matching was inconclusive
            if (dev > 0)
            {
                foreach (GpuProfile p in Profiles)
                {
                    if (p.DeviceIdMatches(dev))
                        return p;
                }
            }

            return null;
        }

        public static GpuIdentity BuildIdentity(string gpuName, string pnpId, GpuProfile profile)
        {
            GpuIdentity i = new GpuIdentity();
            i.Name = gpuName ?? "";
            i.PnpId = pnpId ?? "";
            i.VendorId = ParseHexTokenUInt(pnpId, "VEN_", 4);
            i.DeviceId = ParseHexTokenUInt(pnpId, "DEV_", 4);
            i.SubsystemId = ParseHexTokenUInt(pnpId, "SUBSYS_", 8);
            i.RevisionId = ParseHexTokenUInt(pnpId, "REV_", 2);
            i.ProfileId = profile != null ? profile.Id : "";

            if (profile == null)
                i.Detection = "GPU name did not match a supported RTX 40 / 50 Series Laptop profile.";
            else if (profile.DeviceIdMatches((int)i.DeviceId))
                i.Detection = "Exact model name + reviewed PCI device ID.";
            else if (i.DeviceId > 0)
                i.Detection = "Exact model name + live PCI identity; device ID is validated by the runtime/VBIOS resolvers.";
            else
                i.Detection = "Exact model name; PCI DEV token was not available.";
            return i;
        }

        public static GpuProfile[] All()
        {
            return (GpuProfile[])Profiles.Clone();
        }


        private static uint ParseHexTokenUInt(string text, string token, int digits)
        {
            if (String.IsNullOrEmpty(text)) return 0;
            int p = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (p < 0 || p + token.Length + digits > text.Length) return 0;
            uint value;
            if (UInt32.TryParse(text.Substring(p + token.Length, digits), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                return value;
            return 0;
        }
        private static int ParseHexToken(string text, string token, int digits)
        {
            if (String.IsNullOrEmpty(text)) return 0;
            int p = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (p < 0 || p + token.Length + digits > text.Length) return 0;
            int value;
            if (Int32.TryParse(text.Substring(p + token.Length, digits), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                return value;
            return 0;
        }
    }
}
