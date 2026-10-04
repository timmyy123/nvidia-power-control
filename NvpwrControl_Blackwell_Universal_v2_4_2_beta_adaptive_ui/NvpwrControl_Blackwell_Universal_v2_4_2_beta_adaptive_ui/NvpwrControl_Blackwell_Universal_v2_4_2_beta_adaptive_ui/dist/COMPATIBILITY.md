# Compatibility matrix — Unified Ada & Blackwell Beta v2.4

## UI power profiles

### RTX 40 Series (Ada Lovelace) Laptop GPUs
| SKU | Baseline / Stock | UI range | Step | Status |
|---|---:|---:|---:|---|
| RTX 4050 Laptop | 115 W (alt: 95/105 W) | 115–140 W | 5 W | Unlocked range |
| RTX 4060 Laptop | 140 W (alt: 115/120 W) | 120–150 W | 5 W | Unlocked range |
| RTX 4070 Laptop | 140 W (alt: 115/120 W) | 120–150 W | 5 W | Unlocked range |
| RTX 4080 Laptop | 175 W (alt: 150 W) | 150–225 W | 5 W | Unlocked range |
| RTX 4090 Laptop | 175 W (alt: 150 W) | 150–250 W | 5 W | Unlocked range |

### RTX 50 Series (Blackwell) Laptop GPUs
| SKU | Baseline / Stock | UI range | Step | Built-in physical reference |
|---|---:|---:|---:|---|
| RTX 5050 Laptop | 115 W | 115–140 W | 5 W | stock only |
| RTX 5060 Laptop | 115 W | 115–140 W | 5 W | stock only |
| RTX 5070 Laptop | 115 W | 115–140 W | 5 W | stock only |
| RTX 5070 Ti Laptop | 140 W | 140–180 W | 5 W | 145 W clean user-mode; 160 W previously stability-tested |
| RTX 5080 Laptop | 175 W | 175–250 W | 5 W | stock only in generic backend |
| RTX 5090 Laptop | 175 W | 175–250 W | 5 W | stock only in generic backend |

A profile range is not a thermal/electrical guarantee. Unknown device levels remain experimental.

## Driver resolver

Known exact 617.14 reference hashes are built in. Unknown driver builds require a unique UMD transport pattern, A630 Board Power validation, E633 same-value no-op on an idle GPU, and unchanged public CURRENT readback. Exact UMD/KMD hash pairs are cached only after success. Failure at any stage locks CURRENT writes.

## VBIOS resolver v3

Known exact reference: RTX 5070 Ti VBIOS `98.05.4E.00.07`, normalized shadow MAX `0x580F4`.

For an unknown VBIOS the app automatically attempts a read-only NVFlash dump once per launch, then:

- enumerates NVIDIA PCI option-ROM images via `55 AA` + `PCIR`;
- matches the legacy code-type-0 image to the live GPU `DEV_xxxx`;
- scans the matched image for the validated Blackwell Power Budget v0x40 layout (`0x38/0x66`);
- accepts only one plausible Board-Power record with MAX equal to the SKU stock profile;
- computes shadow offset relative to that exact legacy image;
- caches only the exact GPU PnP + VBIOS result in resolver-v3 cache.

Wrong-ROM identity, zero matches, multiple matches, or a different table layout locks MAX writes.

Telemetry availability does not gate power writes.

## v2.4 semantic policy gate

Before enabling power writes the app cross-checks the detected GPU/PCI identity, driver resolver state, VBIOS resolver state and public CURRENT/MAX readback. Inconsistent/foreign states fail closed. Runtime SET operations use readback postconditions; CURRENT attempts a rollback to the captured baseline if the postcondition fails.

The resolver does not copy Unbound's kernel writer. Unbound-inspired improvements are limited to semantic identity checks, coherent-state classification, fail-closed behavior and transactional verification.
