using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NvpwrControlBlackwell
{
    internal static class NvApiTuner
    {
        private const uint IdPstatesGet = 0x6FF81213;
        private const uint IdPstatesSet = 0x0F4DAE6B;
        private const uint PstatesVersion = 0x00021CF8;
        private const int PstatesSize = 7416;
        private const uint PstatesSetV1Version = 0x00011C94;
        private const int PstatesSetV1Size = 7316;
        private const int PstatesHeader = 20;
        private const int PstatesCount = 16;
        private const int PstateSize = 456;
        private const int PstateClocks = 8;
        private const int ClockSize = 44;
        private const int ClocksOff = 8;
        private const int VoltagesOff = 8 + PstateClocks * ClockSize;
        private const int VoltageSize = 24;
        private const uint DomainGraphics = 0;
        private const uint DomainMemory = 4;
        private const uint DomainCoreVoltage = 0;

        private const uint IdClkGet = 0xF58938F5;
        private const uint IdClkSet = 0xD14B69CF;
        private const uint ClkVersion = 0x000261A4;
        private const int ClkBufSize = 0x13000;
        private const uint ClkMask = 0xFF;
        private const uint ClkMarker = 0x0F;
        private const int ClkOffFreq = 0x114;
        private const int ClkOffMsvdd = 0x11C;
        private const uint IdClkMeasure = 0x527FC458;
        private const uint ClkMeasureVersion = 0x0001000C;
        private const uint XbarMeasureMask = 0x2;

        private const uint IdPropInfo = 0xE826E4F0;
        private const uint IdPropGet = 0xCBFF71D0;
        private const uint IdPropSet = 0xEF3D20EA;
        private const uint PropInfoVersion = 0x00015798;
        private const uint PropControlVersion = 0x0001075C;
        private const int PropBufSize = 0x20000;
        private const uint PropMask = 0xFF;
        private const int PropControlFallbackRatio = 0x68;
        private const uint DefaultRatioRaw = 0xE660;

        private const uint IdVfInfo = 0x507B4B59;
        private const uint IdVfControlGet = 0x23F1B133;
        private const uint VfInfoVersion = 0x0001182C;
        private const uint VfControlVersion = 0x00012420;
        private const int VfInfoSize = 6188;
        private const int VfControlSize = 9248;

        private const uint IdAdcInfo = 0x68789E2A;
        private const uint IdAdcStatus = 0x43D9B26A;
        private const uint AdcInfoVersion = 0x000209F0;
        private const uint AdcStatusVersion = 0x000109C8;
        private const int AdcInfoSize = 2544;
        private const int AdcStatusSize = 2504;

        private sealed class PstatesFields
        {
            public bool Core;
            public int CoreDelta;
            public int CoreCur, CoreMin, CoreMax;
            public bool Memory;
            public int MemDelta;
            public int MemCur, MemMin, MemMax;
            public bool Nvvdd;
            public int VoltDelta;
            public int VoltCur, VoltMin, VoltMax;
        }

        private sealed class XbarFields
        {
            public int Base, Stride;
            public uint Index = 1;
            public int FreqKHz;
            public int MsvddUv;
        }

        private sealed class PropFields
        {
            public uint Raw;
            public int Offset = PropControlFallbackRatio;
        }

        private sealed class RollbackState
        {
            public bool Pstates;
            public int CoreKHz, MemKHz, NvvddUv;
            public bool NvvddPresent;
            public bool Xbar;
            public byte[] XbarBuffer;
            public bool Prop;
            public byte[] PropBuffer;
        }

        public static TunerState Probe(bool allowMsvdd)
        {
            TunerState state = new TunerState();
            using (NvApiSession s = new NvApiSession())
            {
                string e;
                if (!s.Open(out e))
                {
                    state.Error = e;
                    return state;
                }
                ProbeWithSession(s, state, allowMsvdd);
                return state;
            }
        }

        public static OperationResult Apply(TuneRequest request, bool allowMsvdd, out TunerState post)
        {
            post = new TunerState();
            using (NvApiSession s = new NvApiSession())
            {
                string error;
                if (!s.Open(out error)) return OperationResult.Fail(error);
                RollbackState rb = new RollbackState();
                try
                {
                    bool needP = request.SetCore || request.SetMemory || request.SetNvvdd;
                    bool needX = request.SetXbar || request.SetMsvdd;
                    bool needR = request.SetRatio;
                    CaptureRollback(s, rb, needP, needX, needR);

                    if (needP)
                    {
                        byte[] b;
                        PstatesFields f;
                        if (!ReadPstates(s, out b, out f, out error)) throw new InvalidOperationException(error);
                        if (request.SetCore)
                        {
                            if (!f.Core) throw new InvalidOperationException("Core offset is not exposed by Pstates20.");
                            int khz = MHzToKHz(request.CoreMHz);
                            if (khz < f.CoreMin || khz > f.CoreMax) throw new InvalidOperationException("Core offset is outside the driver-reported range.");
                            PutI32(b, f.CoreDelta, khz);
                        }
                        if (request.SetMemory)
                        {
                            if (!f.Memory) throw new InvalidOperationException("Memory offset is not exposed by Pstates20.");
                            int khz = MHzToKHz(request.MemoryMHz);
                            if (khz < f.MemMin || khz > f.MemMax) throw new InvalidOperationException("Memory offset is outside the driver-reported range.");
                            PutI32(b, f.MemDelta, khz);
                        }
                        if (request.SetNvvdd)
                        {
                            if (!f.Nvvdd) throw new InvalidOperationException("NVVDD offset is not exposed by Pstates20.");
                            int uv = MvToUv(request.NvvddMv);
                            if (uv < f.VoltMin || uv > f.VoltMax) throw new InvalidOperationException("NVVDD offset is outside the driver-reported range.");
                            PutI32(b, f.VoltDelta, uv);
                        }
                        if (!SetPstatesBuffer(s, b, out error)) throw new InvalidOperationException(error);

                        byte[] rbBuf; PstatesFields rf;
                        if (!ReadPstates(s, out rbBuf, out rf, out error)) throw new InvalidOperationException("Pstates readback failed: " + error);
                        if (request.SetCore && (!rf.Core || rf.CoreCur != MHzToKHz(request.CoreMHz))) throw new InvalidOperationException("Core offset readback mismatch.");
                        if (request.SetMemory && (!rf.Memory || rf.MemCur != MHzToKHz(request.MemoryMHz))) throw new InvalidOperationException("Memory offset readback mismatch.");
                        if (request.SetNvvdd && (!rf.Nvvdd || rf.VoltCur != MvToUv(request.NvvddMv))) throw new InvalidOperationException("NVVDD offset readback mismatch.");
                    }

                    if (needX)
                    {
                        byte[] b;
                        XbarFields f;
                        if (!ReadXbar(s, out b, out f, out error)) throw new InvalidOperationException(error);
                        int entry = f.Base + (int)f.Index * f.Stride;
                        if (request.SetXbar)
                        {
                            if (request.XbarMHz < -1000 || request.XbarMHz > 1000) throw new InvalidOperationException("XBAR offset must be within -1000..+1000 MHz.");
                            PutI32(b, entry + ClkOffFreq, MHzToKHz(request.XbarMHz));
                        }
                        if (request.SetMsvdd)
                        {
                            if (!allowMsvdd) throw new InvalidOperationException("Separate MSVDD control is not enabled for this GPU profile.");
                            if (request.MsvddMv < -100 || request.MsvddMv > 100) throw new InvalidOperationException("MSVDD offset must be within -100..+100 mV.");
                            PutI32(b, entry + ClkOffMsvdd, MvToUv(request.MsvddMv));
                        }
                        if (!SetXbarBuffer(s, b, out error)) throw new InvalidOperationException(error);

                        byte[] rbBuf; XbarFields rf;
                        if (!ReadXbar(s, out rbBuf, out rf, out error)) throw new InvalidOperationException("XBAR/MSVDD readback failed: " + error);
                        if (request.SetXbar && rf.FreqKHz != MHzToKHz(request.XbarMHz)) throw new InvalidOperationException("XBAR offset readback mismatch.");
                        if (request.SetMsvdd && rf.MsvddUv != MvToUv(request.MsvddMv)) throw new InvalidOperationException("MSVDD offset readback mismatch.");
                    }

                    if (needR)
                    {
                        if (request.GpcXbarRatio < 0.0 || request.GpcXbarRatio > 2.0) throw new InvalidOperationException("GPC:XBAR ratio must be within 0.0..2.0.");
                        byte[] b; PropFields f;
                        if (!ValidateAndReadProp(s, out b, out f, out error)) throw new InvalidOperationException(error);
                        uint raw = RatioToRaw(request.GpcXbarRatio);
                        PutU32(b, f.Offset, raw);
                        if (!SetPropBuffer(s, b, out error)) throw new InvalidOperationException(error);
                        byte[] rbBuf; PropFields rf;
                        if (!ValidateAndReadProp(s, out rbBuf, out rf, out error)) throw new InvalidOperationException("GPC:XBAR ratio readback failed: " + error);
                        if (rf.Raw != raw) throw new InvalidOperationException("GPC:XBAR ratio readback mismatch.");
                    }

                    ProbeWithSession(s, post, allowMsvdd);
                    AppLog.Write("OC apply success");
                    return OperationResult.Ok("Tuning applied and readback verified.");
                }
                catch (Exception ex)
                {
                    string rollbackError;
                    RestoreRollback(s, rb, out rollbackError);
                    ProbeWithSession(s, post, allowMsvdd);
                    string msg = ex.Message;
                    if (!String.IsNullOrEmpty(rollbackError)) msg += " | rollback: " + rollbackError;
                    AppLog.Write("OC apply failed: " + msg);
                    return OperationResult.Fail(msg);
                }
            }
        }

        public static OperationResult ResetFactory(bool allowMsvdd, out TunerState post)
        {
            TunerState s = Probe(allowMsvdd);
            TuneRequest r = new TuneRequest();
            if (s.CoreMHz.Supported) { r.SetCore = true; r.CoreMHz = 0; }
            if (s.MemoryMHz.Supported) { r.SetMemory = true; r.MemoryMHz = 0; }
            if (s.NvvddMv.Supported) { r.SetNvvdd = true; r.NvvddMv = 0; }
            if (s.XbarWritable) { r.SetXbar = true; r.XbarMHz = 0; }
            if (s.MsvddWritable) { r.SetMsvdd = true; r.MsvddMv = 0; }
            if (s.RatioWritable) { r.SetRatio = true; r.GpcXbarRatio = 0.9; }
            return Apply(r, allowMsvdd, out post);
        }

        private static void ProbeWithSession(NvApiSession s, TunerState state, bool allowMsvdd)
        {
            state.NvapiReady = true;
            string e;
            byte[] pb; PstatesFields pf;
            if (ReadPstates(s, out pb, out pf, out e))
            {
                if (pf.Core)
                {
                    state.CoreMHz.Supported = true;
                    state.CoreMHz.Current = KHzToMHz(pf.CoreCur);
                    state.CoreMHz.Min = KHzToMHz(pf.CoreMin);
                    state.CoreMHz.Max = KHzToMHz(pf.CoreMax);
                }
                if (pf.Memory)
                {
                    state.MemoryMHz.Supported = true;
                    state.MemoryMHz.Current = KHzToMHz(pf.MemCur);
                    state.MemoryMHz.Min = KHzToMHz(pf.MemMin);
                    state.MemoryMHz.Max = KHzToMHz(pf.MemMax);
                }
                if (pf.Nvvdd)
                {
                    state.NvvddMv.Supported = true;
                    state.NvvddMv.Current = UvToMv(pf.VoltCur);
                    state.NvvddMv.Min = UvToMv(pf.VoltMin);
                    state.NvvddMv.Max = UvToMv(pf.VoltMax);
                }
            }
            else state.Error += e + "; ";

            byte[] xb; XbarFields xf;
            if (ReadXbar(s, out xb, out xf, out e))
            {
                state.XbarWritable = true;
                state.XbarMHz = KHzToMHz(xf.FreqKHz);
                state.XbarEntryBase = (uint)xf.Base;
                state.XbarEntryStride = (uint)xf.Stride;
                state.XbarDomainIndex = xf.Index;
                state.MsvddWritable = allowMsvdd;
                state.MsvddMv = UvToMv(xf.MsvddUv);
                int physical;
                if (MeasureXbar(s, out physical)) state.XbarPhysicalMHz = physical;
            }
            else state.Error += e + "; ";

            byte[] rb; PropFields rf;
            if (ValidateAndReadProp(s, out rb, out rf, out e))
            {
                state.RatioWritable = true;
                state.GpcXbarRatioRaw = rf.Raw;
                state.GpcXbarRatio = rf.Raw / 65536.0;
            }
            else state.Error += e + "; ";

            ProbeExtraReadOnly(s, state);
        }

        private static bool ReadPstates(NvApiSession s, out byte[] b, out PstatesFields f, out string error)
        {
            error = ""; f = new PstatesFields(); b = new byte[PstatesSize];
            NvApiSession.GpuBufferDelegate get = s.GetGpuBuffer(IdPstatesGet);
            if (get == null) { error = "Pstates20 GET interface missing"; return false; }
            PutU32(b, 0, PstatesVersion);
            int rc = CallBuffer(get, s.Gpu, b);
            if (rc != 0) { error = "Pstates20 GET failed rc=" + rc.ToString(); return false; }
            if (U32(b, 0) != PstatesVersion) { error = "Pstates20 version/layout mismatch"; return false; }

            uint np = Math.Min(U32(b, 8), (uint)PstatesCount);
            uint nc = Math.Min(U32(b, 12), (uint)PstateClocks);
            uint nv = Math.Min(U32(b, 16), 4u);
            int p0 = -1;
            for (uint p = 0; p < np; p++)
            {
                int po = PstatesHeader + (int)p * PstateSize;
                if (po + PstateSize > b.Length) break;
                if (U32(b, po) == 0u) { p0 = po; break; }
            }
            if (p0 < 0) { error = "Pstates20 P0 record was not found"; return false; }

            for (uint c = 0; c < nc; c++)
            {
                int co = p0 + ClocksOff + (int)c * ClockSize;
                if (co + ClockSize > b.Length) break;
                uint domain = U32(b, co);
                int cur = I32(b, co + 12), mn = I32(b, co + 16), mx = I32(b, co + 20);
                if (mn > mx) continue;
                if (domain == DomainGraphics)
                {
                    f.Core = true; f.CoreDelta = co + 12; f.CoreCur = cur; f.CoreMin = mn; f.CoreMax = mx;
                }
                else if (domain == DomainMemory)
                {
                    f.Memory = true; f.MemDelta = co + 12; f.MemCur = cur; f.MemMin = mn; f.MemMax = mx;
                }
            }

            for (uint v = 0; v < nv; v++)
            {
                int vo = p0 + VoltagesOff + (int)v * VoltageSize;
                if (vo + VoltageSize > b.Length) break;
                uint domain = U32(b, vo);
                int cur = I32(b, vo + 12), mn = I32(b, vo + 16), mx = I32(b, vo + 20);
                if (domain == DomainCoreVoltage && mn <= mx && (mn != 0 || mx != 0))
                {
                    f.Nvvdd = true; f.VoltDelta = vo + 12; f.VoltCur = cur; f.VoltMin = mn; f.VoltMax = mx;
                    break;
                }
            }
            return true;
        }

        private static bool SetPstatesBuffer(NvApiSession s, byte[] getBuffer, out string error)
        {
            error = "";
            NvApiSession.GpuBufferDelegate set = s.GetGpuBuffer(IdPstatesSet);
            if (set == null) { error = "Pstates20 SET interface missing"; return false; }

            uint np = Math.Min(U32(getBuffer, 8), (uint)PstatesCount);
            uint nc = Math.Min(U32(getBuffer, 12), (uint)PstateClocks);
            uint nv = Math.Min(U32(getBuffer, 16), 4u);
            int p0 = -1;
            for (uint p = 0; p < np; p++)
            {
                int po = PstatesHeader + (int)p * PstateSize;
                if (po + PstateSize <= getBuffer.Length && U32(getBuffer, po) == 0u) { p0 = po; break; }
            }
            if (p0 < 0) { error = "Pstates20 SET: P0 record missing"; return false; }

            bool hasCore = false, hasMem = false, hasVolt = false;
            int core = 0, mem = 0, volt = 0;
            for (uint c = 0; c < nc; c++)
            {
                int co = p0 + ClocksOff + (int)c * ClockSize;
                if (co + ClockSize > getBuffer.Length) break;
                uint d = U32(getBuffer, co);
                if (d == DomainGraphics) { hasCore = true; core = I32(getBuffer, co + 12); }
                if (d == DomainMemory) { hasMem = true; mem = I32(getBuffer, co + 12); }
            }
            for (uint v = 0; v < nv; v++)
            {
                int vo = p0 + VoltagesOff + (int)v * VoltageSize;
                if (vo + VoltageSize > getBuffer.Length) break;
                if (U32(getBuffer, vo) == DomainCoreVoltage) { hasVolt = true; volt = I32(getBuffer, vo + 12); break; }
            }

            byte[] setb = new byte[PstatesSetV1Size];
            PutU32(setb, 0, PstatesSetV1Version);
            PutU32(setb, 8, 1);
            uint clockCount = 0;
            if (hasCore)
            {
                int co = PstatesHeader + ClocksOff + (int)clockCount++ * ClockSize;
                PutU32(setb, co, DomainGraphics); PutI32(setb, co + 12, core);
            }
            if (hasMem)
            {
                int co = PstatesHeader + ClocksOff + (int)clockCount++ * ClockSize;
                PutU32(setb, co, DomainMemory); PutI32(setb, co + 12, mem);
            }
            PutU32(setb, 12, clockCount);
            if (hasVolt)
            {
                PutU32(setb, 16, 1);
                int vo = PstatesHeader + VoltagesOff;
                PutU32(setb, vo, DomainCoreVoltage); PutI32(setb, vo + 12, volt);
            }

            int rc = CallBuffer(set, s.Gpu, setb);
            if (rc != 0) { error = "Pstates20 SET failed rc=" + rc.ToString(); return false; }
            return true;
        }

        private static bool ReadXbar(NvApiSession s, out byte[] b, out XbarFields f, out string error)
        {
            error = ""; f = new XbarFields(); b = new byte[ClkBufSize];
            NvApiSession.GpuBufferDelegate get = s.GetGpuBuffer(IdClkGet);
            NvApiSession.GpuBufferDelegate set = s.GetGpuBuffer(IdClkSet);
            if (get == null || set == null) { error = "XBAR/MSVDD GET/SET interface missing"; return false; }
            PutU32(b, 0, ClkVersion); PutU32(b, 8, ClkMask);
            int rc = CallBuffer(get, s.Gpu, b);
            if (rc != 0) { error = "ClockDomains GET failed rc=" + rc.ToString(); return false; }
            if (U32(b, 0) != ClkVersion) { error = "ClockDomains version mismatch"; return false; }

            int baseOff, stride;
            if (!FindRepeatingDwordLayout(b, ClkMarker, out baseOff, out stride)) { error = "ClockDomains entry layout was not uniquely established"; return false; }
            if (stride != 0x304) { error = "ClockDomains entry stride is not audited 0x304"; return false; }

            uint idx = 1;
            List<uint> nonzero = new List<uint>();
            for (uint i = 0; i < 32; i++)
            {
                int e = baseOff + (int)i * stride;
                if (e + ClkOffMsvdd + 4 > b.Length) break;
                if (I32(b, e + ClkOffFreq) != 0 || I32(b, e + ClkOffMsvdd) != 0) nonzero.Add(i);
            }
            if (nonzero.Count == 1) idx = nonzero[0];
            int entry = baseOff + (int)idx * stride;
            if (entry + ClkOffMsvdd + 4 > b.Length) { error = "XBAR entry outside ClockDomains buffer"; return false; }
            if (U32(b, entry) != ClkMarker) { error = "XBAR entry marker missing"; return false; }

            f.Base = baseOff; f.Stride = stride; f.Index = idx;
            f.FreqKHz = I32(b, entry + ClkOffFreq);
            f.MsvddUv = I32(b, entry + ClkOffMsvdd);
            return true;
        }

        private static bool SetXbarBuffer(NvApiSession s, byte[] b, out string error)
        {
            error = "";
            NvApiSession.GpuBufferDelegate set = s.GetGpuBuffer(IdClkSet);
            if (set == null) { error = "ClockDomains SET interface missing"; return false; }
            int rc = CallBuffer(set, s.Gpu, b);
            if (rc != 0) { error = "ClockDomains SET failed rc=" + rc.ToString(); return false; }
            return true;
        }

        private static bool MeasureXbar(NvApiSession s, out int mhz)
        {
            mhz = 0;
            NvApiSession.GpuBufferDelegate fn = s.GetGpuBuffer(IdClkMeasure);
            if (fn == null) return false;
            byte[] b = new byte[12];
            PutU32(b, 0, ClkMeasureVersion); PutU32(b, 4, XbarMeasureMask);
            if (CallBuffer(fn, s.Gpu, b) != 0) return false;
            mhz = (int)(U32(b, 8) / 1000u);
            return true;
        }

        private static bool ValidateAndReadProp(NvApiSession s, out byte[] ctrl, out PropFields f, out string error)
        {
            error = ""; f = new PropFields(); ctrl = new byte[PropBufSize];
            NvApiSession.GpuBufferDelegate infoFn = s.GetGpuBuffer(IdPropInfo);
            NvApiSession.GpuBufferDelegate getFn = s.GetGpuBuffer(IdPropGet);
            NvApiSession.GpuBufferDelegate setFn = s.GetGpuBuffer(IdPropSet);
            if (infoFn == null || getFn == null || setFn == null) { error = "GPC:XBAR propagation interfaces missing"; return false; }

            byte[] info = new byte[PropBufSize]; PutU32(info, 0, PropInfoVersion);
            if (CallBuffer(infoFn, s.Gpu, info) != 0 || U32(info, 0) != PropInfoVersion) { error = "Propagation GET_INFO validation failed"; return false; }
            int relationCount = 0;
            for (int off = 0; off + 16 <= info.Length; off += 4)
            {
                if (U32(info, off) != 0u) continue;
                if (info[off + 4] != 0u || info[off + 5] != 1u || info[off + 6] != 1u) continue;
                if (U32(info, off + 8) != DefaultRatioRaw) continue;
                relationCount++;
            }
            if (relationCount != 1) { error = "GPC->XBAR relation was not uniquely resolved"; return false; }

            PutU32(ctrl, 0, PropControlVersion); PutU32(ctrl, 4, PropMask);
            int rc = CallBuffer(getFn, s.Gpu, ctrl);
            if (rc != 0 || U32(ctrl, 0) != PropControlVersion) { error = "Propagation GET_CONTROL failed rc=" + rc.ToString(); return false; }
            int ratioOff = PropControlFallbackRatio;
            uint raw = U32(ctrl, ratioOff);
            if (raw > 2u * 65536u)
            {
                int found = -1, count = 0;
                for (int p = 0; p + 4 <= ctrl.Length; p += 4)
                {
                    if (U32(ctrl, p) == DefaultRatioRaw) { found = p; count++; }
                }
                if (count != 1) { error = "Propagation ratio field was not uniquely resolved"; return false; }
                ratioOff = found; raw = U32(ctrl, ratioOff);
            }
            if (raw > 2u * 65536u) { error = "Propagation ratio outside 0.0..2.0"; return false; }
            f.Raw = raw; f.Offset = ratioOff;
            return true;
        }

        private static bool SetPropBuffer(NvApiSession s, byte[] b, out string error)
        {
            error = "";
            NvApiSession.GpuBufferDelegate fn = s.GetGpuBuffer(IdPropSet);
            if (fn == null) { error = "Propagation SET interface missing"; return false; }
            int rc = CallBuffer(fn, s.Gpu, b);
            if (rc != 0) { error = "Propagation SET failed rc=" + rc.ToString(); return false; }
            return true;
        }

        private static void ProbeExtraReadOnly(NvApiSession s, TunerState state)
        {
            NvApiSession.GpuBufferDelegate infoFn = s.GetGpuBuffer(IdVfInfo);
            byte[] vfMask = null;
            if (infoFn != null)
            {
                byte[] b = new byte[VfInfoSize]; PutU32(b, 0, VfInfoVersion);
                if (CallBuffer(infoFn, s.Gpu, b) == 0 && U32(b, 0) == VfInfoVersion)
                {
                    state.VfInfoAvailable = true; state.VfInfoVersion = U32(b, 0);
                    vfMask = new byte[Math.Min(32, b.Length - 4)]; Buffer.BlockCopy(b, 4, vfMask, 0, vfMask.Length);
                }
            }
            if (vfMask != null)
            {
                NvApiSession.GpuBufferDelegate ctrl = s.GetGpuBuffer(IdVfControlGet);
                if (ctrl != null)
                {
                    byte[] b = new byte[VfControlSize]; PutU32(b, 0, VfControlVersion);
                    Buffer.BlockCopy(vfMask, 0, b, 4, Math.Min(vfMask.Length, 32));
                    if (CallBuffer(ctrl, s.Gpu, b) == 0 && U32(b, 0) == VfControlVersion)
                    {
                        state.VfControlReadable = true; state.VfControlVersion = U32(b, 0);
                    }
                }
            }

            uint adcMask = 0;
            NvApiSession.GpuBufferDelegate adcInfo = s.GetGpuBuffer(IdAdcInfo);
            if (adcInfo != null)
            {
                byte[] b = new byte[AdcInfoSize]; PutU32(b, 0, AdcInfoVersion);
                if (CallBuffer(adcInfo, s.Gpu, b) == 0 && U32(b, 0) == AdcInfoVersion)
                {
                    state.AdcInfoAvailable = true; adcMask = U32(b, 4); state.AdcDeviceMask = adcMask;
                }
            }
            if (adcMask != 0)
            {
                NvApiSession.GpuBufferDelegate adcStatus = s.GetGpuBuffer(IdAdcStatus);
                if (adcStatus != null)
                {
                    byte[] b = new byte[AdcStatusSize]; PutU32(b, 0, AdcStatusVersion); PutU32(b, 4, adcMask);
                    if (CallBuffer(adcStatus, s.Gpu, b) == 0 && U32(b, 0) == AdcStatusVersion) state.AdcStatusAvailable = true;
                }
            }
        }

        private static void CaptureRollback(NvApiSession s, RollbackState rb, bool needP, bool needX, bool needR)
        {
            string e;
            if (needP)
            {
                byte[] b; PstatesFields f;
                if (ReadPstates(s, out b, out f, out e) && f.Core && f.Memory)
                {
                    rb.Pstates = true; rb.CoreKHz = f.CoreCur; rb.MemKHz = f.MemCur;
                    rb.NvvddPresent = f.Nvvdd; if (f.Nvvdd) rb.NvvddUv = f.VoltCur;
                }
            }
            if (needX)
            {
                byte[] b; XbarFields f;
                if (ReadXbar(s, out b, out f, out e)) { rb.Xbar = true; rb.XbarBuffer = (byte[])b.Clone(); }
            }
            if (needR)
            {
                byte[] b; PropFields f;
                if (ValidateAndReadProp(s, out b, out f, out e)) { rb.Prop = true; rb.PropBuffer = (byte[])b.Clone(); }
            }
        }

        private static bool RestoreRollback(NvApiSession s, RollbackState rb, out string error)
        {
            error = ""; bool ok = true; string e;
            if (rb.Prop && rb.PropBuffer != null)
                if (!SetPropBuffer(s, (byte[])rb.PropBuffer.Clone(), out e)) { ok = false; error += "Propagation: " + e + "; "; }
            if (rb.Xbar && rb.XbarBuffer != null)
                if (!SetXbarBuffer(s, (byte[])rb.XbarBuffer.Clone(), out e)) { ok = false; error += "XBAR/MSVDD: " + e + "; "; }
            if (rb.Pstates)
            {
                byte[] b; PstatesFields f;
                if (ReadPstates(s, out b, out f, out e))
                {
                    if (f.Core) PutI32(b, f.CoreDelta, rb.CoreKHz);
                    if (f.Memory) PutI32(b, f.MemDelta, rb.MemKHz);
                    if (f.Nvvdd && rb.NvvddPresent) PutI32(b, f.VoltDelta, rb.NvvddUv);
                    if (!SetPstatesBuffer(s, b, out e)) { ok = false; error += "Pstates: " + e + "; "; }
                }
                else { ok = false; error += "Pstates GET: " + e + "; "; }
            }
            return ok;
        }

        private static bool FindRepeatingDwordLayout(byte[] b, uint marker, out int baseOff, out int stride)
        {
            baseOff = 0; stride = 0;
            List<int> hits = new List<int>();
            for (int off = 0x100; off + 4 <= b.Length; off += 4) if (U32(b, off) == marker) hits.Add(off);
            List<int> auditedRuns = new List<int>();
            foreach (int h in hits)
            {
                if (h >= 0x304 && U32(b, h - 0x304) == marker) continue;
                int count = 1;
                for (int n = h + 0x304; n + 4 <= b.Length; n += 0x304)
                {
                    if (U32(b, n) == marker) count++; else break;
                }
                if (count >= 2) auditedRuns.Add(h);
            }
            if (auditedRuns.Count == 1) { baseOff = auditedRuns[0]; stride = 0x304; return true; }
            return false;
        }

        private static uint RatioToRaw(double ratio)
        {
            if (Math.Abs(ratio - 0.9) < 1e-8) return DefaultRatioRaw;
            return (uint)Math.Round(ratio * 65536.0);
        }

        private static int CallBuffer(NvApiSession.GpuBufferDelegate fn, IntPtr gpu, byte[] b)
        {
            IntPtr p = Marshal.AllocHGlobal(b.Length);
            try
            {
                Marshal.Copy(b, 0, p, b.Length);
                int rc = fn(gpu, p);
                Marshal.Copy(p, b, 0, b.Length);
                return rc;
            }
            finally { Marshal.FreeHGlobal(p); }
        }

        private static uint U32(byte[] b, int off) { return BitConverter.ToUInt32(b, off); }
        private static int I32(byte[] b, int off) { return BitConverter.ToInt32(b, off); }
        private static void PutU32(byte[] b, int off, uint v) { Buffer.BlockCopy(BitConverter.GetBytes(v), 0, b, off, 4); }
        private static void PutI32(byte[] b, int off, int v) { Buffer.BlockCopy(BitConverter.GetBytes(v), 0, b, off, 4); }
        private static int KHzToMHz(int x) { return x / 1000; }
        private static int MHzToKHz(int x)
        {
            long v = (long)x * 1000L;
            if (v > Int32.MaxValue) return Int32.MaxValue;
            if (v < Int32.MinValue) return Int32.MinValue;
            return (int)v;
        }
        private static int UvToMv(int x) { return x / 1000; }
        private static int MvToUv(int x)
        {
            long v = (long)x * 1000L;
            if (v > Int32.MaxValue) return Int32.MaxValue;
            if (v < Int32.MinValue) return Int32.MinValue;
            return (int)v;
        }
    }
}
