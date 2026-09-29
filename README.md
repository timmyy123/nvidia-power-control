# NVIDIA Laptop GPU Power Limit Control (Unified RTX 40 & 50 Series Tuner)

> Low-level power management tuner and TDP unlocker for **NVIDIA GeForce RTX 40 Series** (Ada Lovelace) and **RTX 50 Series** (Blackwell) Laptop GPUs.

---

## Overview

Modern gaming and workstation laptops enforce strict Total Graphics Power (TGP) ceilings through internal NVIDIA driver policies and Dynamic Boost limits. Standard overclocking utilities like MSI Afterburner can adjust core and memory clock offsets, but they cannot adjust the TGP ceiling beyond the OEM factory limits on laptop GPUs.

**NvpwrControl** interfaces directly with the live NVIDIA kernel driver (`nvlddmkm.sys`) power-policy objects in system memory to raise the maximum power limit above factory OEM caps, unlocking sustained performance under heavy workloads.

---

## Supported Hardware & Power Target Ranges

### RTX 40 Series (Ada Lovelace) Laptop GPUs
| GPU | Baseline / Stock Range | Target Power Limit Range | Step |
|---|---|---|---|
| **RTX 4090 Laptop GPU** | 115W – 175W | **150W – 250W** | 5W |
| **RTX 4080 Laptop GPU** | 115W – 175W | **150W – 225W** | 5W |
| **RTX 4070 Laptop GPU** | 100W – 140W | **120W – 150W** | 5W |
| **RTX 4060 Laptop GPU** | 100W – 140W | **120W – 150W** | 5W |
| **RTX 4050 Laptop GPU** | 95W – 115W | **115W – 140W** | 5W |

### RTX 50 Series (Blackwell) Laptop GPUs
| GPU | Baseline / Stock Range | Target Power Limit Range | Step |
|---|---|---|---|
| **RTX 5090 Laptop GPU** | 150W – 175W | **175W – 225W** | 5W |
| **RTX 5080 Laptop GPU** | 150W – 175W | **175W – 225W** | 5W |
| **RTX 5070 Ti Laptop GPU** | 115W – 140W | **145W – 180W** | 5W |
| **RTX 5070 Laptop GPU** | 115W – 140W | **145W – 180W** | 5W |
| **RTX 5060 Laptop GPU** | 100W – 115W | **120W – 140W** | 5W |

---

## Driver Requirements

- Validated on **NVIDIA Driver 616.92** (`nvlddmkm.sys` PE timestamp `0x6A9B4070`, size `0x06D3E000`).
- The kernel driver validates exact driver binary structures before mutating any memory offsets, failing closed if signatures do not match.

---

---

## Quick Start Guide

You can run NvpwrControl using either of two methods:
1. **Method 1: Secure Boot Compatible Mode (Recommended)** — Keeps Secure Boot **ENABLED**, Test-Signing **OFF**, loads driver on-the-fly, and restores DSE so multiplayer anti-cheats (Warzone, Battlefield, Vanguard, EAC) work without issue.
2. **Method 2: Legacy Test-Signing Mode** — Requires disabling Secure Boot in BIOS and enabling Windows Test Mode.

---

### Method 1: Secure Boot Compatible Mode (Recommended for Anti-Cheat Games)

This method uses KDU (Kernel Driver Utility) to temporarily disable Driver Signature Enforcement (DSE 0) on-the-fly while Secure Boot remains **ON** in UEFI/BIOS. Once the GPU power limits are programmed into hardware/driver memory, `Nvpwr.sys` is unloaded and DSE is restored (DSE 6), leaving the system completely clean.

#### Prerequisites (One-Time Setup)
1. **Secure Boot in BIOS**: Leave **ENABLED** (or turn it back ON if previously disabled).
2. **Test Mode**: Keep **OFF** (`bcdedit /set testsigning off`).
3. **Core Isolation / Memory Integrity**: Must be **OFF** (Windows Security -> Device Security -> Core Isolation -> Memory integrity -> Off).
4. **Vulnerable Driver Blocklist**: Must be **OFF** in Windows 11:
   - Right-click **`disable-vulnerable-driver-blocklist.cmd`** and select **Run as administrator**.
   - **Restart your PC**.
5. **Antivirus**: Add this folder to Windows Defender exclusions (or temporarily disable Real-Time Protection during launch), as Defender flags KDU by signature.

#### Launching & Applying Power Limit
- **Option A (Interactive GUI)**:
  1. Double-click or right-click **`launch-with-secureboot.cmd`** -> **Run as administrator**.
  2. The script temporarily disables DSE and launches `NvpwrControl.exe`.
  3. Select your desired Target Power Limit (e.g., 160W) and click **Apply**.
  4. (Optional) Adjust Core or Memory clock offsets.
  5. Close the `NvpwrControl` window.
  6. The script automatically unloads `Nvpwr.sys` from kernel memory and restores DSE (6).
  7. Launch your games (Call of Duty Warzone, Battlefield 6/2042, etc.) — anti-cheats will detect Secure Boot enabled and clean integrity!

- **Option B (Fast Command-Line / One-Shot)**:
  1. Right-click **`apply-power-secureboot.cmd`** -> **Run as administrator**, or run via CMD:
     ```cmd
     apply-power-secureboot.cmd 5070ti 160
     ```
  2. It disables DSE, programs the power limit via `NvpwrCtl`, unloads the driver, and restores DSE within 2 seconds.

---

### Method 2: Legacy Test-Signing Mode (Secure Boot Disabled)

#### Step 1: Disable Secure Boot in BIOS/UEFI
1. Restart your laptop and press **Del** (or **F2**) to enter BIOS.
2. Navigate to the **Security** or **Boot** settings.
3. Set **Secure Boot** to **Disabled**.
4. Press **F10** to save changes and restart your laptop.

#### Step 2: Enable Windows Test Mode & Trust Certificate
1. Open the release folder.
2. Right-click **`install-cert-and-enable-testmode.cmd`** and select **Run as administrator**.
3. **Restart your PC**.

#### Step 3: Run the Tuner & Apply Desired Power
1. Right-click **`NvpwrControl.exe`** and select **Run as administrator**.
2. Select your desired target from the dropdown and click **Apply**.

---

## Built-In NVAPI Overclocking (User-Mode)

In addition to TGP unlocking, `NvpwrControl` includes direct user-mode NVAPI tuning:
- **Core Clock Offset**: Up to ±1000 MHz
- **Memory Clock Offset**: Up to ±3000 MHz
- **Telemetry Readout**: Real-time clock domains, P-states, and rail information.

---

## Building from Source

To compile the project from source:

### Prerequisites
- Visual Studio 2022 or Visual Studio 18 with **Desktop development with C++**
- Windows 10/11 SDK and Windows Driver Kit (WDK) 10.0.28000+

### Build Command
Run the included build script in an elevated PowerShell:
```powershell
powershell -ExecutionPolicy Bypass -File .\NvpwrControl_61692_v1_8_0_unified_blackwell_tuner\build.ps1
```
The compiled binaries (`NvpwrControl.exe`, `NvpwrCtl.exe`, `Nvpwr.sys`, and setup scripts) will be output to the `dist\` directory.

---

## Thermal & Electrical Safety Warning

> **WARNING**: Raising laptop GPU power limits increases electrical load and heat output across the GPU die, VRM power delivery, VRAM, and the laptop cooling subsystem.

- Always monitor temperatures (`GPU Temp`, `Hotspot`, `Memory Temp`, and `VRM`) using HWiNFO or GPU-Z.
- Ensure your laptop cooling vents are clean and your AC power adapter has sufficient wattage to sustain higher power draws.
- All modifications are performed at your own risk.

---

## License & Disclaimer

This project is independent research and is not affiliated with, sponsored by, or endorsed by NVIDIA Corporation. NVIDIA, GeForce, and RTX are trademarks of NVIDIA Corporation.
