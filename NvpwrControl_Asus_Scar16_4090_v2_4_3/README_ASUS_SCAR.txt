================================================================================
NVPWR CONTROL - ASUS ROG STRIX SCAR 16/18 RTX 4090 LAPTOP (v2.4.3)
================================================================================

TARGET SYSTEM:
- Laptop: ASUS ROG Strix SCAR 16 / SCAR 18
- GPU: NVIDIA GeForce RTX 4090 Laptop GPU (16 GB)
- Hardware ID: DEV_2757, SUBSYS_213D1043
- Stock VBIOS: 95.03.2b.00.31 (Scar16_4090_stock.rom)
- ROM SHA256: 8af5a8c43973e37d6ec8332b43221df549f466940253dbc1f5fcdd8ef41a4248
- Stock Max Power Budget: 175 W (175000 mW)

ROOT CAUSE OF THE PREVIOUS ERROR:
"Power Budget Board-Power MAX record was not unique for stock 175 W (matches=0, matching legacy images=2)"
1. Table Version Mismatch: The previous VBIOS resolver only looked for Blackwell (0x40)
   tables. Ada Lovelace (RTX 40 series) uses version 0x30 tables with different field
   offsets (Max power is at offset +0x0A rather than +0x15).
2. Dual Legacy Mirror Images: The ASUS SCAR ROM contains dual legacy images (CodeType 0)
   at 0x009400 and 0x0E9400. Each image contains the power record, producing duplicate
   candidates across mirror images.
3. Legacy Image Bounding: The legacy image boundary was previously truncated at the UEFI
   GOP image (0x019200), cutting off the search before reaching the Power Budget table at
   0x09F3EB.

FIX APPLIED:
1. Updated VbiosResolver.cs to support Ada Lovelace 0x30 tables (+0x0A max field offset)
   alongside Blackwell 0x40 tables.
2. Bounded legacy images by the start of the next legacy image (0x0E9400) or end of ROM.
3. Added candidate deduplication by normalized shadow offset (MaxFieldOffset - ImageStart).
4. Both mirror images resolve uniquely to normalized Shadow Offset: 0x960CD.

PRE-RESOLVED GEOMETRY:
- Power Table Raw Offset:    0x09F3EB (Version 0x30, Header 50, Entry Size 83, Count 20)
- Board-Power MAX Record:    Entry 2 at Raw Offset 0x09F4CD
- Legacy Image Base:         0x009400
- Resolved Shadow Offset:    0x960CD (0x09F4CD - 0x009400)
- Validation Status:         Verified (Score: 100, Confidence: HIGH)

HOW TO RUN:
1. Open the folder:
   c:\Users\timmy\Downloads\nvidia-power-control\NvpwrControl_Asus_Scar16_4090_v2_4_3\dist
2. Run NvpwrControl.exe as Administrator (or double click launch-fixed.cmd).
3. The app will immediately recognize the ASUS SCAR 16 RTX 4090 and load the validated
   shadow offset (0x960CD) directly from the pre-cached database!
================================================================================
