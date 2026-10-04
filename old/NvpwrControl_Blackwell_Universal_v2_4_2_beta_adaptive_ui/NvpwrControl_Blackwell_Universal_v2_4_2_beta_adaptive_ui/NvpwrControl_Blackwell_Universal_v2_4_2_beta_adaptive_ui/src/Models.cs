using System;

namespace NvpwrControlBlackwell
{
    internal sealed class GpuIdentity
    {
        public string Name = "";
        public string PnpId = "";
        public uint VendorId;
        public uint DeviceId;
        public uint SubsystemId;
        public uint RevisionId;
        public string ProfileId = "";
        public string Detection = "";

        public string PciSummary()
        {
            string s = "";
            if (VendorId > 0) s += "VEN_" + VendorId.ToString("X4");
            if (DeviceId > 0) s += (s.Length > 0 ? " · " : "") + "DEV_" + DeviceId.ToString("X4");
            if (SubsystemId > 0) s += (s.Length > 0 ? " · " : "") + "SUBSYS_" + SubsystemId.ToString("X8");
            if (RevisionId > 0) s += (s.Length > 0 ? " · " : "") + "REV_" + RevisionId.ToString("X2");
            return s;
        }
    }

    internal sealed class DriverResolution
    {
        public bool CandidateFound;
        public bool Trusted;
        public bool KnownExact;
        public string Source = "";
        public string Reason = "";
        public long TransportRva;
        public string ImplSha256 = "";
        public string KmdSha256 = "";
        public string DeviceKey = "";
    }

    internal sealed class VbiosResolution
    {
        public bool Resolved;
        public bool KnownExact;
        public string Source = "";
        public string Reason = "";
        public string RomPath = "";
        public string RomSha256 = "";
        public int StockMaxW;
        public int ShadowOffset;
        public int PowerTableRawOffset;
        public int MaxFieldRawOffset;
        public int LegacyImageRawOffset;
        public int RomImageCount;
        public int MatchingRomImageCount;
        public int PcirDeviceId;
        public int CandidateCount;
        public int EntryIndex;
        public string Layout = "";

        // Semantic fingerprint of the selected Board-Power record.  These
        // fields are read-only diagnostics used by the resolver/state machine.
        public int RecordMinMw;
        public int RecordDefaultMw;
        public int RecordMaxMw;
        public int RecordBaseMw;
        public int SemanticScore;
        public string Confidence = "";
    }

    internal sealed class PowerPolicyAssessment
    {
        public string State = "UNRESOLVED";
        public string Reason = "";
        public bool Consistent;
        public double? LiveCurrentW;
        public double? LiveMaxW;
        public int? InstalledOverrideW;
    }

    internal sealed class CompatibilityState
    {
        public bool Supported;
        public bool CurrentWritesReady;
        public bool MaxWritesReady;
        public string Reason = "";
        public string GpuName = "";
        public string GpuPnpId = "";
        public string Vbios = "";
        public string DriverVersion = "";
        public string KmdPath = "";
        public string KmdSha256 = "";
        public string ImplPath = "";
        public string ImplSha256 = "";
        public long ImplBase;
        public GpuProfile Profile;
        public GpuIdentity Identity = new GpuIdentity();
        public DriverResolution Driver = new DriverResolution();
        public VbiosResolution VbiosResolver = new VbiosResolution();
        public PowerPolicyAssessment Policy = new PowerPolicyAssessment();
    }

    internal sealed class PowerState
    {
        public double? CurrentW;
        public double? RequestedW;
        public double? MaxW;
        public string Raw = "";
    }

    internal sealed class TelemetryState
    {
        public double? PowerW;
        public double? GpuTempC;
        public double? HotspotTempC;
        public double? MemoryTempC;
        public double? UtilizationPct;
        public double? CoreClockMHz;
        public double? MemoryClockMHz;
        public double? VoltageV;
        public PowerState Power = new PowerState();
        public string Error = "";
    }

    internal sealed class TuneRange
    {
        public bool Supported;
        public int Current;
        public int Min;
        public int Max;
    }

    internal sealed class TunerState
    {
        public bool NvapiReady;
        public string Error = "";
        public TuneRange CoreMHz = new TuneRange();
        public TuneRange MemoryMHz = new TuneRange();
        public TuneRange NvvddMv = new TuneRange();

        public bool XbarWritable;
        public int XbarMHz;
        public int XbarPhysicalMHz;
        public int XbarMinMHz = -1000;
        public int XbarMaxMHz = 1000;
        public uint XbarEntryBase;
        public uint XbarEntryStride;
        public uint XbarDomainIndex = 1;

        public bool MsvddWritable;
        public int MsvddMv;
        public int MsvddMinMv = -100;
        public int MsvddMaxMv = 100;

        public bool RatioWritable;
        public double GpcXbarRatio;
        public uint GpcXbarRatioRaw;

        public bool VfInfoAvailable;
        public bool VfControlReadable;
        public uint VfInfoVersion;
        public uint VfControlVersion;

        public bool AdcInfoAvailable;
        public bool AdcStatusAvailable;
        public uint AdcDeviceMask;
    }

    internal sealed class TuneRequest
    {
        public bool SetCore;
        public int CoreMHz;
        public bool SetMemory;
        public int MemoryMHz;
        public bool SetNvvdd;
        public int NvvddMv;
        public bool SetXbar;
        public int XbarMHz;
        public bool SetMsvdd;
        public int MsvddMv;
        public bool SetRatio;
        public double GpcXbarRatio = 0.9;
    }

    internal sealed class OperationResult
    {
        public bool Success;
        public string Message = "";
        public bool RebootRequired;

        public static OperationResult Ok(string message)
        {
            return new OperationResult { Success = true, Message = message };
        }

        public static OperationResult Fail(string message)
        {
            return new OperationResult { Success = false, Message = message };
        }
    }
}
