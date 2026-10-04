using System.Runtime.InteropServices;

namespace SimpleNvidiaUndervolt;

// Packed layouts for the NVAPI tuning structures. Field order, array sizes and Pack=8 are fixed
// by the driver ABI; the leading uint of each top-level struct is the version word (see
// NvApi.MakeVersion). Reserved/opaque words are named Unknown* and preserved verbatim on a
// read-modify-write so the driver still accepts them.

/// <summary>A signed delta value plus its valid range, in the field's unit (kHz or uV).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct ParameterDelta
{
    public int Value;
    public int ValueMin;
    public int ValueMax;
}

/// <summary>One clock domain within a performance state (NV_GPU_PSTATE20_CLOCK_ENTRY_V1).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct Pstate20ClockEntry
{
    public uint DomainId;
    public uint TypeId;
    public uint Flags;
    public ParameterDelta FreqDeltaKhz;

    // NV_GPU_PSTATE20_CLOCK_DEPENDENT_INFO: a 20-byte union. For a range-type clock these are
    // minFreqKhz, maxFreqKhz, voltageDomain, minVoltageUv, maxVoltageUv. The memory clock's
    // absolute frequency (which moves with an applied memory offset) lives in Data0.
    public uint Data0;
    public uint Data1;
    public uint Data2;
    public uint Data3;
    public uint Data4;
}

/// <summary>One base-voltage domain within a performance state (NV_GPU_PSTATE20_BASE_VOLTAGE_ENTRY_V1).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct Pstate20BaseVoltageEntry
{
    public uint DomainId;
    public uint Flags;
    public uint Value;
    public ParameterDelta ValueDeltaUv;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct Pstate20
{
    public const int MaxClocks = 8;
    public const int MaxBaseVoltages = 4;

    public uint PstateId;
    public uint Flags;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxClocks)]
    public Pstate20ClockEntry[] Clocks;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxBaseVoltages)]
    public Pstate20BaseVoltageEntry[] BaseVoltages;
}

/// <summary>NV_GPU_PERF_PSTATES20_INFO_V1 (version 1, 7316 bytes = 0x11C94 version word).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct Pstates20InfoV1
{
    public const int MaxPstates = 16;

    public uint Version;
    public uint Flags;
    public uint NumPstates;
    public uint NumClocks;
    public uint NumBaseVoltages;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxPstates)]
    public Pstate20[] Pstates;
}

// The V/F curve control table (ClkVfPointsControl, version 1, 9248 bytes) and status (version 1,
// 7208 bytes) are flat arrays of fixed-stride per-point entries that do not match the layout the SDK
// headers imply, so they are read and written as raw bytes in NvApi (see the offset constants there)
// rather than via a marshalled struct.

/// <summary>One public clock domain in NV_GPU_CLOCK_FREQUENCIES (bIsPresent bitfield + frequency).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct ClockFrequencyDomain
{
    public uint IsPresent;     // bit 0: domain populated; rest reserved
    public uint FrequencyKhz;
}

/// <summary>NV_GPU_CLOCK_FREQUENCIES_V2 (264 bytes). <see cref="ClockType"/> selects which clocks the
/// driver reports: 0 = current, 1 = base (factory), 2 = boost. The base clock is independent of any
/// applied offset, so it is how the stock memory clock is recovered while an offset is live.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct ClockFrequenciesV2
{
    public const int MaxDomains = 32;

    public uint Version;
    public uint ClockType;     // low 4 bits: NV_GPU_CLOCK_FREQUENCIES_CLOCK_TYPE; rest reserved

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxDomains)]
    public ClockFrequencyDomain[] Domains;
}

/// <summary>Core voltage boost percentage — Afterburner's "Core Voltage (%)" slider (version 1).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct VoltageBoostPercentV1
{
    public const int MaxUnknown = 8;

    public uint Version;
    public uint Percent;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxUnknown)]
    public uint[] Unknown;
}
