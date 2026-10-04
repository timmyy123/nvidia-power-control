using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace SimpleNvidiaUndervolt;

/// <summary>
/// Minimal interop layer over NVIDIA's NVAPI, exposing what is needed to read and reset the
/// clock/voltage tuning that MSI Afterburner (and similar tools) program into the driver.
///
/// NVAPI exposes a single export, <c>nvapi_QueryInterface</c>, which maps a function id to the
/// real entry point. The ids and packed-struct layouts here are not part of the public SDK; they
/// are the long-stable definitions used by NVIDIA overclocking tools. Each
/// tuning struct begins with a <c>version</c> word encoding
/// <c>sizeof(struct) | (versionNumber &lt;&lt; 16)</c>, which the driver validates.
/// </summary>
internal static class NvApi
{
    // --- Function ids (arguments to nvapi_QueryInterface) ---
    private const uint ID_Initialize = 0x0150E828;
    private const uint ID_Unload = 0xD22BDD7E;
    private const uint ID_GetErrorMessage = 0x6C2D048C;
    private const uint ID_EnumPhysicalGPUs = 0xE5AC921F;
    private const uint ID_GPU_GetFullName = 0xCEEE8E9F;
    private const uint ID_GPU_GetPCIIdentifiers = 0x2DDFB66E;

    private const uint ID_GPU_GetPstates20 = 0x6FF81213;
    private const uint ID_GPU_SetPstates20 = 0x0F4DAE6B;
    private const uint ID_GPU_GetClockBoostLock = 0xE440B867; // PerfClientLimitsGetStatus (diagnostics only)
    private const uint ID_GPU_GetClkDomainsInfo = 0x64B43A6A; // a.k.a. ClkDomainsGetInfo — offset ranges
    private const uint ID_GPU_GetClockBoostMask = 0x507B4B59; // a.k.a. ClkVfPointsGetInfo
    private const uint ID_GPU_GetClockBoostTable = 0x23F1B133; // a.k.a. ClkVfPointsGetControl
    private const uint ID_GPU_SetClockBoostTable = 0x0733E009; // a.k.a. ClkVfPointsSetControl
    private const uint ID_GPU_GetCoreVoltageBoostPercent = 0x9DF23CA1;
    private const uint ID_GPU_SetCoreVoltageBoostPercent = 0xB9306D9B;

    /// <summary>ClockClientClkVfPointsGetStatus — the live, full V/F curve.</summary>
    private const uint ID_GPU_GetVfCurveStatus = 0x21537AD4;

    /// <summary>ClientVoltRailsGetStatus — the live core rail voltage.</summary>
    private const uint ID_GPU_GetVoltRailsStatus = 0x465F9BCF;

    /// <summary>NvAPI_GPU_GetAllClockFrequencies — current/base/boost public clocks.</summary>
    private const uint ID_GPU_GetAllClockFrequencies = 0xDCB616C3;

    /// <summary>NvAPI_GPU_GetArchInfo — the architecture id (documented public API).</summary>
    private const uint ID_GPU_GetArchInfo = 0xD8265D24;

    /// <summary>NvAPI_GPU_GetThermalSettings — current temperatures. Takes a sensor-index arg.</summary>
    private const uint ID_GPU_GetThermalSettings = 0xE3640A56;

    /// <summary>ClientPowerTopologyGetStatus — live board power draw (per-mille of percent of TGP).</summary>
    private const uint ID_GPU_GetPowerTopologyStatus = 0xEDCF624E;

    public const uint CLOCK_FREQ_TYPE_CURRENT = 0;
    public const uint CLOCK_FREQ_TYPE_BASE = 1;
    public const uint CLOCK_FREQ_TYPE_BOOST = 2;

    private const int NVAPI_MAX_PHYSICAL_GPUS = 64;
    private const int NVAPI_SHORT_STRING_MAX = 64;

    public const uint CLOCK_DOMAIN_GRAPHICS = 0;
    public const uint CLOCK_DOMAIN_MEMORY = 4;
    public const uint VOLTAGE_DOMAIN_CORE = 0;

    [DllImport("nvapi64", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr NvAPI_QueryInterface(uint id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NoArgDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EnumPhysicalGpusDelegate([Out] IntPtr[] handles, out int count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate int GetErrorMessageDelegate(int status, StringBuilder message);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate int GetFullNameDelegate(IntPtr gpu, StringBuilder name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetPciIdentifiersDelegate(IntPtr gpu,
        out uint deviceId, out uint subSystemId, out uint revisionId, out uint extDeviceId);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GpuStructDelegate(IntPtr gpu, IntPtr data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GpuThermalDelegate(IntPtr gpu, uint sensorIndex, IntPtr data);

    private static T GetDelegate<T>(uint id) where T : Delegate
    {
        IntPtr address = NvAPI_QueryInterface(id);
        if (address == IntPtr.Zero)
        {
            throw new CliError($"NVAPI function 0x{id:X8} is not available in this driver.");
        }

        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    private static int MakeVersion(int structSize, int versionNumber)
        => structSize | (versionNumber << 16);

    private static void Check(int status, string action)
    {
        if (status != 0)
        {
            throw new CliError($"{action} failed: {DescribeStatus(status)}");
        }
    }

    private static string DescribeStatus(int status)
    {
        try
        {
            var message = new StringBuilder(NVAPI_SHORT_STRING_MAX);
            if (GetDelegate<GetErrorMessageDelegate>(ID_GetErrorMessage)(status, message) == 0)
            {
                return $"{message} ({status})";
            }
        }
        catch
        {
            // Fall through to the raw code.
        }

        return $"NVAPI status {status}";
    }

    public static void Initialize()
        => Check(GetDelegate<NoArgDelegate>(ID_Initialize)(), "NvAPI_Initialize");

    public static void Unload()
    {
        try
        {
            GetDelegate<NoArgDelegate>(ID_Unload)();
        }
        catch
        {
            // Unload is best-effort cleanup.
        }
    }

    public static IntPtr[] EnumeratePhysicalGpus()
    {
        var handles = new IntPtr[NVAPI_MAX_PHYSICAL_GPUS];
        Check(GetDelegate<EnumPhysicalGpusDelegate>(ID_EnumPhysicalGPUs)(handles, out int count),
            "NvAPI_EnumPhysicalGPUs");
        return handles.Take(count).ToArray();
    }

    private static string GetFullName(IntPtr gpu)
    {
        var name = new StringBuilder(NVAPI_SHORT_STRING_MAX);
        Check(GetDelegate<GetFullNameDelegate>(ID_GPU_GetFullName)(gpu, name), "NvAPI_GPU_GetFullName");
        return name.ToString();
    }

    /// <summary>The GPU's full name, or <c>&lt;unknown&gt;</c> if the driver won't report it — for the
    /// human-facing headers that shouldn't fail a command just because the name read did.</summary>
    public static string SafeFullName(IntPtr gpu)
    {
        try
        {
            return GetFullName(gpu);
        }
        catch (Exception)
        {
            return "<unknown>";
        }
    }

    /// <summary>The GPU's PCI identifiers — the exact card model (device, subsystem/board vendor,
    /// revision, extended device). Deliberately not the bus/slot ids: moving the same card to another
    /// slot doesn't change its V/F curve.</summary>
    public static (uint DeviceId, uint SubSystemId, uint RevisionId, uint ExtDeviceId)
        GetPciIdentifiers(IntPtr gpu)
    {
        Check(GetDelegate<GetPciIdentifiersDelegate>(ID_GPU_GetPCIIdentifiers)(
            gpu, out uint device, out uint subSystem, out uint revision, out uint extDevice),
            "NvAPI_GPU_GetPCIIdentifiers");
        return (device, subSystem, revision, extDevice);
    }

    // --- Generic versioned-struct get/set ---

    private static T GetStruct<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        IntPtr gpu, uint functionId, int versionNumber, string action)
        where T : struct
    {
        int size = Marshal.SizeOf<T>();
        IntPtr buffer = AllocZeroed(size);
        try
        {
            Marshal.WriteInt32(buffer, MakeVersion(size, versionNumber));
            Check(GetDelegate<GpuStructDelegate>(functionId)(gpu, buffer), action);
            return Marshal.PtrToStructure<T>(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>Writes a versioned struct: the same round-trip as <see cref="GetStructInOut{T}"/>, with
    /// whatever the driver leaves in the buffer discarded.</summary>
    private static void SetStruct<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        IntPtr gpu, uint functionId, T value, int versionNumber, string action)
        where T : struct
        => GetStructInOut(gpu, functionId, value, versionNumber, action);

    /// <summary>Like <see cref="GetStruct{T}"/>, but marshals <paramref name="input"/> into the buffer
    /// first so caller-supplied input fields (e.g. a clock-type selector) survive into the call. The
    /// version word is always re-stamped over whatever the struct carried.</summary>
    private static T GetStructInOut<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        IntPtr gpu, uint functionId, T input, int versionNumber, string action)
        where T : struct
    {
        int size = Marshal.SizeOf<T>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(input, buffer, false);
            Marshal.WriteInt32(buffer, MakeVersion(size, versionNumber));
            Check(GetDelegate<GpuStructDelegate>(functionId)(gpu, buffer), action);
            return Marshal.PtrToStructure<T>(buffer);
        }
        finally
        {
            Marshal.DestroyStructure<T>(buffer);
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>An <c>AllocHGlobal</c> buffer zero-filled, so the driver sees deterministic reserved
    /// fields and padding; the caller frees it.</summary>
    private static IntPtr AllocZeroed(int size)
    {
        IntPtr buffer = Marshal.AllocHGlobal(size);
        Marshal.Copy(new byte[size], 0, buffer, size);
        return buffer;
    }

    /// <summary>Stamps a request mask over the words following the version — which entries the caller
    /// asks the driver to fill (or apply, on a set).</summary>
    private static void WriteRequestMask(IntPtr buffer, byte[] mask)
        => Marshal.Copy(mask, 0, buffer + 4, mask.Length);

    private static byte[] AllOnesMask(int words)
        => Enumerable.Repeat((byte)0xFF, words * 4).ToArray();

    // --- Performance states (memory / core clock offsets, base voltage) ---

    public static Pstates20InfoV1 GetPstates20(IntPtr gpu)
        => GetStruct<Pstates20InfoV1>(gpu, ID_GPU_GetPstates20, 1, "NvAPI_GPU_GetPstates20");

    /// <summary>Writes the P0 graphics/memory clock and core-voltage offsets. The memory offset is
    /// applied here (it is absent from the curve control table), so this is what resets memory.
    ///
    /// Note the read/write asymmetry: <see cref="GetPstates20"/> reports the memory clock as an
    /// absolute frequency (Afterburner's offset already folded in), but the driver still tracks that
    /// offset internally as a P0 delta. Writing the delta as 0 here therefore returns the absolute
    /// clock to its stock base — e.g. a +440 MHz offset reading 14441 MHz drops back to 14001 MHz.</summary>
    public static void SetPstate0Offsets(IntPtr gpu, int graphicsDeltaKhz, int memoryDeltaKhz, int coreVoltageDeltaUv)
    {
        var info = NewPstates20();
        info.NumPstates = 1;
        info.NumClocks = 2;
        info.NumBaseVoltages = 1;

        ref Pstate20 p0 = ref info.Pstates[0];
        p0.PstateId = 0; // P0 - the 3D performance state Afterburner edits
        p0.Clocks[0].DomainId = CLOCK_DOMAIN_GRAPHICS;
        p0.Clocks[0].FreqDeltaKhz.Value = graphicsDeltaKhz;
        p0.Clocks[1].DomainId = CLOCK_DOMAIN_MEMORY;
        p0.Clocks[1].FreqDeltaKhz.Value = memoryDeltaKhz;
        p0.BaseVoltages[0].DomainId = VOLTAGE_DOMAIN_CORE;
        p0.BaseVoltages[0].ValueDeltaUv.Value = coreVoltageDeltaUv;

        SetStruct(gpu, ID_GPU_SetPstates20, info, 1, "NvAPI_GPU_SetPstates20");
    }

    // --- Public clock frequencies (current / base / boost) ---

    /// <summary>Reads a public clock domain's frequency (kHz) for the given clock type. The base type
    /// reports the factory clock independent of any applied offset; returns 0 if not populated.</summary>
    public static uint GetClockFrequencyKhz(IntPtr gpu, uint clockType, uint domain)
    {
        var input = new ClockFrequenciesV2
        {
            ClockType = clockType,
            Domains = new ClockFrequencyDomain[ClockFrequenciesV2.MaxDomains],
        };
        var result = GetStructInOut(gpu, ID_GPU_GetAllClockFrequencies, input, 2, "NvAPI_GPU_GetAllClockFrequencies");

        ClockFrequencyDomain entry = result.Domains[domain];
        return (entry.IsPresent & 1) != 0 ? entry.FrequencyKhz : 0;
    }

    // --- Live telemetry (voltage / temperature / power) ---

    /// <summary>The live core-rail voltage (uV). In ClientVoltRailsGetStatus the value sits at +0x28.</summary>
    public static uint GetCoreVoltageUv(IntPtr gpu)
    {
        byte[] bytes = ReadRaw(gpu, ID_GPU_GetVoltRailsStatus, 1, 76, 256, requestMaskWords: 0);
        return BitConverter.ToUInt32(bytes, 0x28);
    }

    /// <summary>The live GPU core temperature (degrees C), from sensor 0 of GetThermalSettings (V2).</summary>
    public static int GetCoreTemperatureC(IntPtr gpu)
    {
        const int size = 68; // NV_GPU_THERMAL_SETTINGS_V2: version, count, sensor[3] x 20 bytes
        IntPtr buffer = AllocZeroed(size);
        try
        {
            Marshal.WriteInt32(buffer, MakeVersion(size, 2));
            Check(GetDelegate<GpuThermalDelegate>(ID_GPU_GetThermalSettings)(gpu, 0, buffer),
                "NvAPI_GPU_GetThermalSettings");
            return Marshal.ReadInt32(buffer, 0x14); // sensor[0].currentTemp
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>The live board power draw as a percentage of the total power limit (TGP). The power
    /// topology reports channel 0 (total) in thousandths of a percent, so the value is scaled by 1000.</summary>
    public static double GetPowerPercent(IntPtr gpu)
    {
        byte[] bytes = ReadRaw(gpu, ID_GPU_GetPowerTopologyStatus, 1, 72, 256, requestMaskWords: 0);
        return BitConverter.ToUInt32(bytes, 0x10) / 1000.0; // entry[0].value
    }

    // --- V/F curve: status (effective curve) and control (per-point frequency deltas) ---
    //
    // Both buffers are flat arrays of per-point entries, one per voltage anchor (80-130ish of them,
    // by generation); the control table is shifted one anchor relative to the status (see the
    // off-by-one note below). The byte offsets below were derived against a known
    // Afterburner curve; the typed-struct layout the SDK headers imply does not match the driver, so
    // these are handled as raw bytes. The status buffer reports the *effective* curve (it reflects an
    // applied offset); the control buffer holds only the editable freq deltas (0 at stock).
    //
    // Requests for either buffer carry a bitmask (the words after the version) naming the point
    // slots to fill, and the driver fails the whole call with NVAPI_ERROR when the mask names a
    // slot the card doesn't have - so the mask is read from the driver (GetVfPointsMask) rather
    // than assumed.
    private const int CONTROL_TABLE_SIZE = 9248; // ClkVfPointsGetControl / SetControl, version 1
    public const int CtrlEntryBase = 0x64;
    public const int CtrlEntryStride = 36;
    public const int CtrlDeltaOffset = 0x18; // signed kHz frequency delta within an entry

    private const int StatusCurveSize = 7208; // ClkVfPointsGetStatus, version 1
    public const int StatusEntryBase = 0x40;
    public const int StatusEntryStride = 28;
    public const int StatusTypeOffset = 0x04; // point domain: 0 = core, 1 = memory
    public const int StatusFreqOffset = 0x08; // kHz
    public const int StatusVoltOffset = 0x0C; // uV

    // What a real V/F curve reads as - one physical judgment each, shared by the curve decode
    // (GetVfCurve), the layout detection (CurveLayout) and the readability checks (GpuTuning) so the
    // three can't drift apart. The voltage window brackets any real core rail; the boost floor is low
    // enough that a power-limited mobile part still passes, while a wholesale transitional collapse
    // (every clock a few hundred MHz) fails.
    public const int MinCoreVoltUv = 300_000;
    public const int MaxCoreVoltUv = 1_300_000;
    public const int MinBoostClockKhz = 1_200_000;

    private const int CLOCK_MASKS_SIZE = 6188; // ClkVfPointsGetInfo (a.k.a. GetClockBoostMask), version 1

    /// <summary>The width of the request-mask field in the ClkVfPoints structs: the 32 bytes after
    /// the version word, one bit per point slot (a 5090 populates 132 bits).</summary>
    private const int VfPointsMaskBytes = 32;

    /// <summary>The card's own VF-point mask - one bit per populated curve-point slot, read from
    /// ClkVfPointsGetInfo (which itself takes no input mask). The status and control calls reject a
    /// request mask naming slots the card doesn't have, and the slot count varies by generation
    /// (~103 on a GTX 1080, 132 on a 5090), so every status/control request carries this mask.
    /// This is also the first ClkVfPoints call on every path, so a card with no curve interface at
    /// all fails here - diagnosed by architecture rather than left as a raw driver error.</summary>
    private static byte[] GetVfPointsMask(IntPtr gpu)
    {
        try
        {
            byte[] bytes = ReadRaw(gpu, ID_GPU_GetClockBoostMask, 1, CLOCK_MASKS_SIZE, CLOCK_MASKS_SIZE,
                requestMaskWords: 0);
            return bytes[4..(4 + VfPointsMaskBytes)];
        }
        catch (CliError error)
        {
            throw CurveUnavailableDiagnosis(ArchitectureId(gpu)) is { } diagnosis
                ? new CliError($"{diagnosis} ({error.Message})")
                : error;
        }
    }

    /// <summary>NV_GPU_ARCHITECTURE_GP100 - Pascal, the first generation with a per-point V/F curve
    /// (GPU Boost 3.0). Ids are ordered by generation, so anything below has no curve at all.</summary>
    internal const uint ARCHITECTURE_PASCAL = 0x130;

    /// <summary>The GPU's architecture id (NV_GPU_ARCHITECTURE_ID: 0x110 Maxwell, 0x130 Pascal,
    /// 0x160 Turing, 0x1B0 Blackwell, ...), or null when the driver won't report it - this feeds an
    /// error diagnosis, which the lookup's own failure must not displace.</summary>
    private static uint? ArchitectureId(IntPtr gpu)
    {
        try
        {
            // NV_GPU_ARCH_INFO_V2: version + architecture + implementation + revision.
            return BitConverter.ToUInt32(ReadRaw(gpu, ID_GPU_GetArchInfo, 2, 16, 16, requestMaskWords: 0), 4);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>What a failed VF-point read means for this card, or null when the architecture is
    /// unknown and the driver's own error has to speak for itself. Pre-Pascal generations have no
    /// ClkVfPoints interface, so tuning them is impossible rather than unported; from Pascal on the
    /// interface exists, but some SKUs (laptop parts have been seen) ship with it stubbed.</summary>
    internal static string? CurveUnavailableDiagnosis(uint? architectureId)
    {
        if (architectureId is not { } id)
        {
            return null;
        }

        if (id < ARCHITECTURE_PASCAL)
        {
            string family = id switch
            {
                >= 0x110 => "Maxwell",
                >= 0xE0 => "Kepler",
                >= 0xC0 => "Fermi",
                _ => $"architecture 0x{id:X}",
            };
            return $"This is a {family}-generation GPU: the per-point V/F curve this tool tunes was "
                + "introduced with Pascal (GTX 10xx, GPU Boost 3.0), so earlier cards cannot be "
                + "tuned this way at all.";
        }

        return "The driver doesn't expose the V/F curve interface on this GPU (seen on some laptop "
            + "parts).";
    }

    /// <summary>The raw status (effective curve) buffer, for re-detecting the curve layout when a read
    /// fails the plausibility check (the offsets likely don't fit this GPU). Read-only.</summary>
    public static byte[] ReadVfCurveStatusRaw(IntPtr gpu)
        => ReadRaw(gpu, ID_GPU_GetVfCurveStatus, 1, StatusCurveSize, StatusCurveSize, GetVfPointsMask(gpu));

    /// <summary>Reads the V/F curve as ordered (millivolt, megahertz) points. The voltage of each
    /// anchor is stable, but the frequency column reflects the <em>live</em> curve: the lowest anchors
    /// pin at a floor clock at idle, and around a power-state change the whole column can briefly read
    /// back collapsed. The point ordering aligns by index with the control-table deltas. Entries are
    /// walked by ascending voltage, independent of the frequency, so the voltage map survives any read.
    /// Only core-domain points count: the memory-domain slots that follow them (slot 127 on a 5090,
    /// slot 80 on Pascal) end the walk outright, so one can't slip in as a curve anchor even with a
    /// voltage that happens to continue the ascent.</summary>
    public static IReadOnlyList<(int Mv, int Mhz)> GetVfCurve(IntPtr gpu)
    {
        byte[] bytes = ReadVfCurveStatusRaw(gpu);

        var points = new List<(int Mv, int Mhz)>();
        int lastVoltUv = 0;
        for (int e = StatusEntryBase; e + StatusEntryStride <= bytes.Length; e += StatusEntryStride)
        {
            if (BitConverter.ToInt32(bytes, e + StatusTypeOffset) != 0)
            {
                break; // a memory-domain point - those slots follow the core anchors
            }

            int freq = BitConverter.ToInt32(bytes, e + StatusFreqOffset);
            int volt = BitConverter.ToInt32(bytes, e + StatusVoltOffset);
            if (volt is < MinCoreVoltUv or > MaxCoreVoltUv)
            {
                break; // past the populated entries (trailing zeros / wrap sentinel)
            }

            // Compare raw microvolts, not the truncated mV: a sub-mV decrease still marks the end of the
            // real array, and truncating first would let such an entry slip through.
            if (points.Count > 0 && volt <= lastVoltUv)
            {
                break; // no longer ascending - end of the real array
            }

            lastVoltUv = volt;
            points.Add((volt / 1000, Math.Max(0, freq) / 1000));
        }

        return points;
    }

    // --- Control-table delta unit ---
    //
    // The status curve's frequencies are plain kHz on every generation, but the control table's
    // delta field is not: on Pascal it is in HALF-kHz units (a field value of 200000 moves the
    // anchor by 100 MHz), while Turing and later use plain kHz. The unit is witnessed read-only
    // through ClkDomainsGetInfo: the graphics domain's delta range reads +/-2,000,000 raw on Pascal
    // and +/-1,000,000 on plain-unit cards, both meaning the universal +/-1000 MHz offset limit.
    // The helpers below read/write true kHz and convert at the field, so nothing else in the
    // codebase sees the unit. (Evidence: Demion/nvapioc and arcnmx/nvapi-rs, both Pascal-era;
    // the +/-1,000,000 range and plain-kHz writes verified on a 5090.)

    private const int CLK_DOMAINS_SIZE = 2344; // ClkDomainsGetInfo, version 1
    private const int DomainsEntryBase = 0x28;
    private const int DomainsEntryStride = 72;
    private const int DomainsTypeOffset = 0x04;     // public clock id: 0 = graphics, 4 = memory
    private const int DomainsRangeMaxOffset = 0x28; // signed, in the control table's delta unit
    private const int DomainsRangeMinOffset = 0x2C;

    /// <summary>The graphics domain's frequency-delta range in raw (control-table) units, from a
    /// ClkDomainsGetInfo buffer: the first graphics-typed entry with a populated range. Null when
    /// none is - the buffer doesn't fit this layout, and no unit can be inferred from it.</summary>
    internal static (int RawMax, int RawMin)? GraphicsDeltaRange(byte[] bytes)
    {
        for (int e = DomainsEntryBase; e + DomainsEntryStride <= bytes.Length; e += DomainsEntryStride)
        {
            int rawMax = BitConverter.ToInt32(bytes, e + DomainsRangeMaxOffset);
            if (BitConverter.ToInt32(bytes, e + DomainsTypeOffset) == 0 && rawMax != 0)
            {
                return (rawMax, BitConverter.ToInt32(bytes, e + DomainsRangeMinOffset));
            }
        }

        return null;
    }

    /// <summary>Raw field units per kHz for the control table's delta field: 2 only on the exact
    /// Pascal signature (+/-2,000,000 raw for the +/-1000 MHz limit) on a Pascal-architecture card,
    /// 1 otherwise. Both conditions are needed: the signature alone would also match a future
    /// plain-unit card that genuinely widens its limit to +/-2000 MHz, and doubling there
    /// overshoots the request. Everything else - Volta included (untested; deliberately outside the
    /// band), and an unreadable architecture - deliberately reads as 1: unscaled deltas are the
    /// safe miss, landing at half depth with the realized-cap report saying so.</summary>
    internal static int CurveDeltaUnitScale((int RawMax, int RawMin)? graphicsRange, uint? architectureId)
        => graphicsRange == (2_000_000, -2_000_000)
           && architectureId is >= ARCHITECTURE_PASCAL and < ARCHITECTURE_VOLTA
            ? 2
            : 1;

    /// <summary>NV_GPU_ARCHITECTURE_GV100 - Volta, the first id past the Pascal band.</summary>
    internal const uint ARCHITECTURE_VOLTA = 0x140;

    /// <summary>The card's control-table delta unit (see <see cref="CurveDeltaUnitScale"/>). A card
    /// where the witness can't be read at all reads as plain units - the pre-witness behavior.</summary>
    private static int GetCurveDeltaUnitScale(IntPtr gpu)
    {
        try
        {
            byte[] bytes = ReadRaw(gpu, ID_GPU_GetClkDomainsInfo, 1, CLK_DOMAINS_SIZE, CLK_DOMAINS_SIZE,
                requestMaskWords: 0);
            return CurveDeltaUnitScale(GraphicsDeltaRange(bytes), ArchitectureId(gpu));
        }
        catch (Exception)
        {
            return 1;
        }
    }

    // The control entry at index j drives the effective frequency of the NEXT curve anchor (status
    // index j+1) — an off-by-one verified empirically (poking control entry j moves status anchor
    // j+1 by the written amount). So the delta for status/curve anchor i lives at control index i-1;
    // anchor 0 (the lowest voltage) has no control entry and cannot be moved. These helpers present
    // deltas already aligned to the curve index so callers can ignore the shift.

    /// <summary>The byte position of curve anchor <paramref name="anchor"/>'s delta field in the
    /// control table (control entry <c>anchor - 1</c>).</summary>
    private static int CtrlDeltaPos(int anchor) => CtrlEntryBase + (anchor - 1) * CtrlEntryStride + CtrlDeltaOffset;

    /// <summary>How many curve anchors the control table can address — the largest delta array
    /// <see cref="SetCurveFreqDeltasKhz"/> accepts (anchor 0 counts toward it but has no entry). A
    /// reset zeroes this many rather than only the anchors visible in one status decode, so a foreign
    /// delta past a truncated read still clears. The quotient is the number of whole entries that fit
    /// the table; +2 counts the one-based anchor of the last entry plus the entry-less anchor 0.</summary>
    public const int MaxCurveAnchors =
        (CONTROL_TABLE_SIZE - CtrlEntryBase - CtrlDeltaOffset - 4) / CtrlEntryStride + 2;

    /// <summary>Reads the per-point frequency deltas (kHz), index-aligned with <see cref="GetVfCurve"/>
    /// (the delta of anchor i is read from control entry i-1).</summary>
    public static int[] GetCurveFreqDeltasKhz(IntPtr gpu, int count)
    {
        int scale = GetCurveDeltaUnitScale(gpu);
        byte[] bytes = ReadRaw(gpu, ID_GPU_GetClockBoostTable, 1, CONTROL_TABLE_SIZE, CONTROL_TABLE_SIZE,
            GetVfPointsMask(gpu));

        var deltas = new int[count];
        for (int i = 1; i < count; i++)
        {
            deltas[i] = BitConverter.ToInt32(bytes, CtrlDeltaPos(i)) / scale;
        }

        return deltas;
    }

    /// <summary>Writes per-point frequency deltas (kHz) via read-modify-write, so reserved fields
    /// round-trip. <paramref name="deltasKhz"/> is index-aligned with the curve (the delta of anchor i
    /// is written to control entry i-1; anchor 0 has no control entry and is ignored).</summary>
    public static void SetCurveFreqDeltasKhz(IntPtr gpu, int[] deltasKhz)
    {
        // The loop below pokes unmanaged memory at computed offsets, where an overrun is heap
        // corruption rather than an exception - so the highest entry must fit the table.
        if (deltasKhz.Length > 1 && CtrlDeltaPos(deltasKhz.Length - 1) + 4 > CONTROL_TABLE_SIZE)
        {
            throw new CliError(
                $"{deltasKhz.Length} curve deltas don't fit the {CONTROL_TABLE_SIZE}-byte control table.");
        }

        int scale = GetCurveDeltaUnitScale(gpu);
        byte[] mask = GetVfPointsMask(gpu);
        IntPtr buffer = AllocZeroed(CONTROL_TABLE_SIZE);
        try
        {
            Marshal.WriteInt32(buffer, MakeVersion(CONTROL_TABLE_SIZE, 1));
            WriteRequestMask(buffer, mask);
            Check(GetDelegate<GpuStructDelegate>(ID_GPU_GetClockBoostTable)(gpu, buffer),
                "NvAPI_GPU_GetClockBoostTable");

            for (int i = 1; i < deltasKhz.Length; i++)
            {
                Marshal.WriteInt32(buffer, CtrlDeltaPos(i), deltasKhz[i] * scale);
            }

            Marshal.WriteInt32(buffer, MakeVersion(CONTROL_TABLE_SIZE, 1));
            WriteRequestMask(buffer, mask);
            Check(GetDelegate<GpuStructDelegate>(ID_GPU_SetClockBoostTable)(gpu, buffer),
                "NvAPI_GPU_SetClockBoostTable");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // --- Core voltage boost percentage (Afterburner "Core Voltage (%)") ---

    public static uint GetCoreVoltageBoostPercent(IntPtr gpu)
        => GetStruct<VoltageBoostPercentV1>(gpu, ID_GPU_GetCoreVoltageBoostPercent, 1,
            "NvAPI_GPU_GetCoreVoltageBoostPercent").Percent;

    public static void SetCoreVoltageBoostPercent(IntPtr gpu, uint percent)
    {
        // Read first so the opaque trailing words round-trip unchanged.
        var value = GetStruct<VoltageBoostPercentV1>(gpu, ID_GPU_GetCoreVoltageBoostPercent, 1,
            "NvAPI_GPU_GetCoreVoltageBoostPercent");
        value.Percent = percent;
        SetStruct(gpu, ID_GPU_SetCoreVoltageBoostPercent, value, 1, "NvAPI_GPU_SetCoreVoltageBoostPercent");
    }

    private static Pstates20InfoV1 NewPstates20()
    {
        var info = new Pstates20InfoV1 { Pstates = new Pstate20[Pstates20InfoV1.MaxPstates] };
        for (int i = 0; i < info.Pstates.Length; i++)
        {
            info.Pstates[i].Clocks = new Pstate20ClockEntry[Pstate20.MaxClocks];
            info.Pstates[i].BaseVoltages = new Pstate20BaseVoltageEntry[Pstate20.MaxBaseVoltages];
        }

        return info;
    }

    // -----------------------------------------------------------------------------------------
    // Diagnostics
    //
    // Raw-buffer helpers for inspecting NVAPI tuning structs (used by the
    // scan / snapshot / diff / probe / extent / curve commands). They deliberately bypass the
    // typed structs above so unknown layouts can be explored safely.
    // -----------------------------------------------------------------------------------------

    /// <summary>The tuning buffers worth scanning/diffing, with the (version, size, request-mask)
    /// each one needs. Sizes that have no managed struct are given as literals.</summary>
    public static IReadOnlyList<(string Name, byte[] Bytes)> ReadRawTuningBuffers(IntPtr gpu)
    {
        // The curve buffers' requests must carry the card's VF-point mask (see GetVfPointsMask).
        // Everything here is best-effort, so when the mask itself can't be read they carry an
        // all-ones 128-bit mask instead - a subset request is accepted (a 5090 has 132 slots), so
        // this keeps the curve buffers scannable on such a card even without the mask call.
        byte[] vfpMask;
        try
        {
            vfpMask = GetVfPointsMask(gpu);
        }
        catch (Exception)
        {
            vfpMask = AllOnesMask(4);
        }

        (string Name, uint Id, int Version, int Size, byte[] Mask)[] specs =
        {
            ("pstates20", ID_GPU_GetPstates20, 1, Marshal.SizeOf<Pstates20InfoV1>(), Array.Empty<byte>()),
            ("curveControl", ID_GPU_GetClockBoostTable, 1, CONTROL_TABLE_SIZE, vfpMask),
            ("curveStatusV1", ID_GPU_GetVfCurveStatus, 1, StatusCurveSize, vfpMask), // same buffer GetVfCurve decodes
            ("voltageLock", ID_GPU_GetClockBoostLock, 2, 780, Array.Empty<byte>()),
            ("clkDomainsInfo", ID_GPU_GetClkDomainsInfo, 1, 2344, Array.Empty<byte>()),
            ("clkVfPointsInfo", ID_GPU_GetClockBoostMask, 1, CLOCK_MASKS_SIZE, Array.Empty<byte>()),
        };

        var result = new List<(string, byte[])>();
        foreach (var spec in specs)
        {
            try
            {
                result.Add((spec.Name, ReadRaw(gpu, spec.Id, spec.Version, spec.Size, spec.Size, spec.Mask)));
            }
            catch (Exception)
            {
                // Skip structures this driver/GPU does not support.
            }
        }

        return result;
    }

    /// <summary>Over-allocation for raw reads whose real struct size is unknown (probe / extent): large
    /// enough to contain anything the driver writes, so an oversized struct can't corrupt the heap.</summary>
    public const int ProbeAllocSize = 262144;

    /// <summary>The largest struct size a version word can claim: <see cref="MakeVersion"/> packs the
    /// size into the low 16 bits, so a larger claim wraps into a garbage word the driver rejects with
    /// a misleading "incompatible version" error.</summary>
    public const int MaxClaimedSize = 0xFFFF;

    /// <summary>Sweeps claimed sizes for one (function, version) and yields each size the driver accepts —
    /// used to discover which struct size/version a function takes. The buffer is over-allocated so an
    /// accepted-but-larger struct cannot overflow into the heap, and a single buffer (re-zeroed between
    /// calls) serves the whole sweep, since a probe tries tens of thousands of sizes.</summary>
    public static IEnumerable<int> ProbeAcceptedSizes(IntPtr gpu, uint functionId, int versionNumber,
        int minSize, int maxSize, int step)
    {
        GpuStructDelegate probe = GetDelegate<GpuStructDelegate>(functionId);
        var zeroes = new byte[ProbeAllocSize];
        IntPtr buffer = Marshal.AllocHGlobal(ProbeAllocSize);
        try
        {
            for (int size = minSize; size <= maxSize; size += step)
            {
                Marshal.Copy(zeroes, 0, buffer, ProbeAllocSize);
                Marshal.WriteInt32(buffer, MakeVersion(size, versionNumber));
                if (probe(gpu, buffer) == 0)
                {
                    yield return size;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>The all-ones-mask form of the raw read, for the diagnostics commands whose mask is a
    /// word count argument. The count is bounded before the mask is materialized: it arrives from a
    /// command-line argument, and a huge value would allocate its full size - or overflow the byte
    /// count outright - if the fit check waited for the built array.</summary>
    public static byte[] ReadRaw(IntPtr gpu, uint functionId, int versionNumber, int claimedSize,
        int allocSize, int requestMaskWords)
        => requestMaskWords < 0 || 4 + (long)requestMaskWords * 4 > allocSize
            ? throw new CliError($"Raw read 0x{functionId:X8}: {requestMaskWords} request-mask words "
                                 + $"don't fit the {allocSize}-byte buffer.")
            : ReadRaw(gpu, functionId, versionNumber, claimedSize, allocSize, AllOnesMask(requestMaskWords));

    /// <summary>Reads a raw buffer for a function. <paramref name="allocSize"/> may exceed
    /// <paramref name="claimedSize"/>: the driver validates the version word against the claimed
    /// size but may write the (larger) real struct, so the extra allocation keeps any overflow in
    /// our own padding instead of corrupting the heap. Returns the full allocation.</summary>
    private static byte[] ReadRaw(IntPtr gpu, uint functionId, int versionNumber, int claimedSize,
        int allocSize, byte[] requestMask)
    {
        // The claimed size and mask width reach here from diagnostics arguments; past the allocation
        // they would let the driver write beyond the buffer, or write the mask out of bounds ourselves
        // - unmanaged heap corruption, not an exception.
        if (claimedSize < 4 || claimedSize > allocSize || 4 + (long)requestMask.Length > allocSize)
        {
            throw new CliError($"Raw read 0x{functionId:X8}: size {claimedSize} or the "
                                + $"{requestMask.Length}-byte request mask don't fit the {allocSize}-byte buffer.");
        }

        if (claimedSize > MaxClaimedSize)
        {
            throw new CliError($"Raw read 0x{functionId:X8}: the version word encodes the claimed "
                                + $"size in 16 bits, so it can be at most {MaxClaimedSize}.");
        }

        IntPtr buffer = AllocZeroed(allocSize);
        try
        {
            Marshal.WriteInt32(buffer, MakeVersion(claimedSize, versionNumber));
            WriteRequestMask(buffer, requestMask);
            Check(GetDelegate<GpuStructDelegate>(functionId)(gpu, buffer), $"raw read 0x{functionId:X8}");
            var bytes = new byte[allocSize];
            Marshal.Copy(buffer, bytes, 0, allocSize);
            return bytes;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
