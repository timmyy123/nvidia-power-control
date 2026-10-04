# Source audit — 2.4.2 Universal Blackwell Beta

Static generation checks:

- C# sources use one namespace: `NvpwrControlBlackwell`.
- WinForms project targets .NET Framework 4.8 / x64 and keeps syntax compatible with the in-box .NET Framework compiler used by `build.ps1`.
- manifest uses `requireAdministrator` and PerMonitorV2 DPI.
- UI shell is a four-row TableLayoutPanel: nav / GPU header / page content / status; page content no longer overlaps the header.
- tuning controls have explicit per-domain labels rather than a generic Target label.
- GPU profiles remain RTX 5050/5060/5070/5070 Ti/5080/5090 Laptop.
- power target lists use 5-W steps in the configured SKU ranges.
- unknown driver writes are fail-closed until semantic no-op validation.
- unknown VBIOS is auto-dumped read-only once per session and stays fail-closed until resolver-v3 validates ROM identity + a unique Power Budget record.
- resolver-v3 matches PCIR Device ID to the live PnP DEV_xxxx and supports multiple chained NVIDIA option-ROM images.
- legacy v2.2 VBIOS cache is not trusted; v2.4 uses `vbios-resolver-cache-v3.txt`.
- foreign/additional romOverride values are not overwritten/removed.
- CURRENT is rejected above live public MAX and under high GPU utilization.
- MAX change reports reboot required; GUI includes Reboot Now.
- Factory Reset removes tuning, stock-restores CURRENT, removes autostart and removes only tool-owned MAX override.
- Core/Memory/NVVDD use Pstates20 capability/range/readback.
- XBAR/MSVDD requires unique audited 0x304 live layout.
- GPC:XBAR requires semantic relationship/readback.
- V/F and ADC/rail remain read-only.
- hotspot is not fabricated from temperature.gpu.tlimit; unavailable genuine sensors remain N/A.

Runtime validation is still required for every previously unseen GPU/VBIOS/driver combination. The resolver validates identity/layout/control-path semantics; it does not prove VRM/cooling stability at raised power.


## 2.4 additions

- responsive centered page roots and two-column grids; maximized state persisted;
- custom PowerSlider for MAX/CURRENT;
- exact live PCI identity parsing including 32-bit SUBSYS;
- VBIOS semantic score/fingerprint stored in resolver-v3 cache;
- live power-policy coherence state machine gates writes;
- CURRENT/MAX write postconditions and rollback paths;
- independent global/button/slider accent palettes plus custom RGB picker;
- multi-size application icon embedded by both build.ps1 and MSBuild.
