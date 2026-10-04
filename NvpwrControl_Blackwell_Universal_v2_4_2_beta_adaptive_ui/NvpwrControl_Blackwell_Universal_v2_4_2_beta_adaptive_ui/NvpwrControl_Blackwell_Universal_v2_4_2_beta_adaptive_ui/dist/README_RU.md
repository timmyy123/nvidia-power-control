# NvpwrControl 2.4.2 — Universal Blackwell Laptop Tuner (Beta)

Универсальная ветка NvpwrControl для мобильных NVIDIA Blackwell. Интерфейс переработан под адаптивное окно: центрированный responsive-layout, Power-слайдеры, верхняя телеметрия, отдельные разделы Power / Tuning / Telemetry / Compatibility / Settings, Graphite/Midnight/Light, независимые цвета общего accent / кнопок / шкал (включая произвольный ColorDialog), RU/EN и Per-Monitor DPI V2.

## Что нового в 2.4.2

- MAX и CURRENT выбираются горизонтальными шкалами с шагом профиля.
- layout центрируется и растягивается до разумной максимальной ширины при изменении окна/Maximize; состояние Maximize сохраняется.
- точная GPU identity включает VEN / DEV / SUBSYS / REV и показывается в Compatibility.
- VBIOS resolver v3 сохраняет семантический fingerprint Board-Power record и confidence score; неоднозначное состояние блокирует запись.
- добавлена policy state machine: STOCK / MAX_PENDING_REBOOT / MAX_ACTIVE_CURRENT_STOCK / TARGET_ACTIVE / CUSTOM_CONSISTENT / INCONSISTENT и др.
- CURRENT SET получил postcondition/readback и попытку rollback к предыдущему CURRENT при несовпадении. MAX registry transaction также восстанавливает предыдущий romOverride при readback failure.
- Compatibility показывает отдельные статусы GPU, Driver/RM, VBIOS/MAX и Policy.
- отдельные цвета общего accent, primary-кнопок и power slider; можно выбрать preset или произвольный RGB через ColorDialog.
- добавлен нормальный multi-size EXE icon.

Идеи semantic resolver/state-machine/transactional verification взяты как архитектурные принципы после анализа Unbound. Kernel-memory writes и `XMGPowerPatch.sys` в NvpwrControl не добавлялись: текущий Power backend сохраняет user-mode NVIDIA RM/romOverride архитектуру.

## Поддерживаемые SKU и диапазоны Power

### RTX 40 Series (Ada Lovelace) Laptop
| GPU | Заводской профиль | Диапазон MAX/CURRENT в UI |
|---|---:|---:|
| RTX 4050 Laptop | 115 W (alt: 95/105 W) | 115–140 W |
| RTX 4060 Laptop | 140 W (alt: 115/120 W) | 120–150 W |
| RTX 4070 Laptop | 140 W (alt: 115/120 W) | 120–150 W |
| RTX 4080 Laptop | 175 W (alt: 150 W) | 150–225 W |
| RTX 4090 Laptop | 175 W (alt: 150 W) | 150–250 W |

### RTX 50 Series (Blackwell) Laptop
| GPU | Заводской профиль | Диапазон MAX/CURRENT в UI |
|---|---:|---:|
| RTX 5050 Laptop | 115 W | 115–140 W |
| RTX 5060 Laptop | 115 W | 115–140 W |
| RTX 5070 Laptop | 115 W | 115–140 W |
| RTX 5070 Ti Laptop | 140 W | 140–180 W |
| RTX 5080 Laptop | 175 W | 175–250 W |
| RTX 5090 Laptop | 175 W | 175–250 W |

Шаг — 5 W.

**Важно:** наличие уровня в меню означает профильный диапазон проекта, а не утверждение о физической стабильности каждого ноутбука. На эталонной RTX 5070 Ti подтверждены clean user-mode 145 W и ранее стабильные 160 W; остальные SKU/уровни требуют проверки на конкретной системе. Значения выше reference-validated уровня получают отдельное предупреждение перед применением.

## Архитектура Power

### MAX — persistent / требует reboot

```text
VBIOS resolver
→ Power Budget v0x40
→ unique stock MAX field
→ normalized NVIDIA ROM-shadow offset
→ romOverride00
→ reboot
→ NVIDIA rebuild policy
→ public Max Power Limit
```

Программа не использует один hardcoded ROM-offset для всех ноутбуков.

Для неизвестного VBIOS v2.4 делает это автоматически один раз за запуск:

1. получает ROM через bundled `tools\nvflash64.exe` только в read-only dump режиме;
2. разбирает все NVIDIA PCI option-ROM images и их `PCIR`;
3. извлекает `DEV_xxxx` текущей GPU из PnP ID и оставляет только legacy code-type-0 image с тем же PCI Device ID;
4. внутри совпавшего image ищет Blackwell Power Budget v0x40 (`header 0x38`, `entry 0x66`);
5. требует ровно один правдоподобный Board-Power record, чей MAX равен stock-профилю SKU;
6. вычисляет normalized shadow offset относительно начала именно найденного legacy image;
7. проверяет reference-invariant для известной RTX 5070 Ti;
8. кэширует результат только для точной связки GPU PnP + VBIOS;
9. только после этого разблокирует MAX write.

Если автоматический dump не удался, остаётся ручной **Select ROM**. Любая неоднозначность/несовпадение PCI Device ID оставляет MAX заблокированным.

Известный эталонный VBIOS RTX 5070 Ti `98.05.4E.00.07` встроен как exact profile (`shadow MAX = 0x580F4`).

### CURRENT — runtime / без reboot

```text
Driver resolver
→ NVIDIA UMD RM transport
→ semantic validation
→ RM 0x2080E633
→ Board Power bit0 / source F7
→ NVIDIA native generator
→ CURRENT
```

Для NVIDIA 617.14 эталонный transport известен. Для неизвестного драйвера программа:

1. pattern-scan'ит executable sections `nvapi64_impl.dll` и требует один transport candidate;
2. НЕ разрешает write сразу;
3. кнопка **Validate new driver** выполняет A630 Board Power validation;
4. затем делает E633 `CURRENT → тот же CURRENT` no-op на idle GPU;
5. проверяет, что публичный CURRENT не изменился;
6. только после успешной semantic validation сохраняет exact UMD/KMD hash pair + RVA в локальный cache.

Если сигнатура или semantics изменились — CURRENT write остаётся заблокированным. Это fail-closed поведение после NVIDIA update.

## Правильный порядок изменения мощности

При изменении MAX GUI показывает жёлтое уведомление и кнопку **Перезагрузить сейчас / Reboot now**.

```text
1. Выбрать MAX.
2. Save MAX.
3. Перезагрузить Windows.
4. Снова открыть NvpwrControl.
5. Убедиться, что NVIDIA показывает новый MAX.
6. Только после этого выставить CURRENT ≤ MAX.
```

CURRENT нужно менять на idle GPU, до запуска FurMark/3DMark/игры.

## Factory reset

`Reset all` выполняет максимально полный возврат:

- Core / Memory / XBAR / MSVDD / NVVDD offsets → заводские/нулевые значения;
- GPC:XBAR → default 0.9, если контрол доступен;
- CURRENT → stock SKU;
- удаление autostart CURRENT;
- удаление только **tool-owned** `romOverride00`;
- если MAX override был удалён, GUI показывает reboot-required banner и позволяет сразу перезагрузить Windows.

Чужие или дополнительные `romOverride*` значения автоматически не удаляются.

## Blackwell tuning

Advanced Tuning работает через private NvAPI и включает capability gates.

- **Core offset** — Pstates20, driver-reported min/max, SET + readback.
- **Memory offset** — Pstates20, driver-reported min/max, SET + readback.
- **NVVDD offset** — только если Pstates20 реально сообщает editable voltage-delta range.
- **XBAR offset** — ClockDomains; dynamic record discovery + audited `0x304` layout + readback.
- **MSVDD offset** — XBAR-domain request; доступен только там, где профиль и live layout это подтверждают.
- **GPC:XBAR ratio** — semantic GPC→XBAR relationship validation + SET/readback.
- **V/F** — read-only capability/readback.
- **ADC / rail** — read-only capability/readback.

Каждая writable OC-операция использует `GET → validate → SET → GET/readback`; при multi-stage ошибке выполняется rollback захваченного baseline.

## Интерфейс

- RU / EN;
- Dark / Light;
- Accent: Purple / Blue / Cyan / Green / Orange / Red;
- пользовательский UI scale: 90 / 100 / 110 / 125%;
- Per-Monitor DPI V2;
- сохранение размеров окна и настроек текущего пользователя;
- верхняя телеметрия: Core / Voltage / Power / GPU Temp / Hotspot (N/A, если корректного датчика нет);
- отдельная Telemetry page;
- Restart NVIDIA device;
- Compatibility report/export и application log.

## Сборка

Windows 10/11 x64, .NET Framework 4.x/4.8.

Самый простой вариант:

```text
build.cmd
```

или PowerShell:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\build.ps1
```

Результат:

```text
dist\NvpwrControl.exe
```

EXE имеет `requireAdministrator` manifest.

## Первый запуск на уже известной RTX 5070 Ti / 617.14

На эталонной системе должны сразу определиться:

```text
GPU      RTX 5070 Ti Laptop
Driver   617.14 exact profile
VBIOS    98.05.4E.00.07 exact profile
CURRENT writes READY
MAX writes READY
```

## Новый NVIDIA driver

После обновления драйвера:

- старый RVA **не используется автоматически**;
- Compatibility покажет candidate/locked state;
- закрой GPU workloads;
- нажми **Validate new driver**;
- только успешный unique-pattern + A630 + E633 no-op разрешит CURRENT write.

Если resolver не может доказать новый transport, пользователь экспортирует Compatibility report для добавления новой сигнатуры в следующую версию программы.

## Новый / неизвестный VBIOS

Compatibility → **Auto-resolve VBIOS**.

В v2.4 `nvflash64.exe` уже находится в `tools\` и используется только для чтения ROM. При неизвестном VBIOS приложение само делает одну попытку auto-resolve на старте. Если dump/структурная проверка не прошли, можно выбрать `.rom/.bin` вручную.

MAX остаётся locked до уникального device-matched структурного разрешения таблицы.

## Логи / cache / recovery

В каталоге программы:

```text
state\driver-resolver-cache.txt
state\vbios-resolver-cache-v3.txt
state\backup-*.reg
state\rom-cache\
```

Application log:

```text
C:\ProgramData\NvpwrControlBlackwell\nvpwr-control.log
```

Registry backup создаётся перед изменением/removal MAX override.

## Текущий статус Beta

Эта сборка уже содержит универсальную архитектуру resolver'ов, но **не объявляет 250 W на любой RTX 5080/5090 физически подтверждённым**. Generic MAX/CURRENT путь должен пройти resolver и live readback на каждом новом SKU/VBIOS. Это намеренно консервативнее, чем просто показывать ползунок и надеяться, что layout одинаковый.


## Исправление v2.2.0

Исправлена совместимость со штатным .NET Framework compiler
`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.

В v2.2.0 три метода содержали `await` внутри `finally`, из-за чего сборка
останавливалась с `CS1984`. В v2.2.0 asynchronous refresh выполняется после
`finally`; power/resolver/tuning logic не менялась.


# v2.2.0 — mVolt-style telemetry/layout pass

## Что исправлено

### Телеметрия
Старый v2.1.x запускал один общий `nvidia-smi --query-gpu=...`.
Если хотя бы одно поле (например `voltage.graphics`) не поддерживалось,
весь запрос падал и UI показывал `N/A` для всех датчиков.

v2.2.0 использует прямую архитектуру того же класса, что обнаружена
при статическом анализе приложенного mVolt:

- NVML:
  - power draw
  - GPU temperature
  - GPU utilization
  - graphics clock
  - memory clock
  - enforced/current power limit
  - power-limit constraints
  - `NVML_FI_DEV_MEMORY_TEMP`
- private/public NVAPI:
  - GPU/VBIOS identity
  - current GPU voltage (`0x465F9BCF`) — только при валидном readback
  - extended internal thermal sensors (`0x65FE3AAD`) — для hotspot candidate

`nvidia-smi` теперь только независимый per-field fallback.
Неподдерживаемый один sensor больше не обнуляет остальные.

### Интерфейс
Исправлена причина узких/обрезанных карточек:
процентные колонки внутри AutoSize TableLayoutPanel заменены на реальные
absolute columns. Базовое окно увеличено до 1320x880, minimum 1180x760.

Доступный пользовательский масштаб:
`80 / 90 / 100 / 110 / 125 / 140 / 150%`.

### NVFlash
Приложенный пользователем `nvflash64.exe` включён неизменённым в:

`tools\nvflash64.exe`

При сборке он копируется в `dist\tools\`.
VBIOS resolver сначала использует canonical read-only syntax:

`nvflash64.exe --save="<rom-path>"`

и затем только fallback syntax.

SHA256 включённого NVFlash:
`c0620a7be0ffdad3782d9ddf238d1a19c31b587cbcfee7036819c8a50e2d89cd`

Перед публичным распространением программы отдельно проверь условия
redistribution NVIDIA NVFlash. Для личной/тестовой сборки helper берётся
из предоставленного пользователем бинарника.


# v2.3.0 — UI layout + VBIOS resolver v2

## UI

- верхняя навигация, GPU header, рабочая область и status bar теперь размещены в фиксированном `TableLayoutPanel`; страницы больше не рисуются под header;
- возвращены полностью видимые заголовки секций на Power / Tuning / Telemetry / Compatibility / Settings;
- увеличены вертикальные отступы;
- в Tuning вместо безымянного `Target` теперь отдельные подписи: **Смещение частоты ядра**, **Смещение частоты памяти**, **Смещение XBAR**, **Смещение MSVDD/NVVDD**, **Целевой GPC:XBAR**;
- Compatibility получил отдельные блоки **Проверка и резольв** и **Диагностический отчёт**.

## VBIOS resolver v2

Resolver больше не предполагает, что в ROM обязательно ровно один legacy image. Он сопоставляет ROM с текущим `PCI\VEN_10DE&DEV_xxxx`, сканирует только подходящие NVIDIA code-type-0 image и принимает MAX offset только при единственном структурно валидном кандидате. Старый resolver-cache не переиспользуется: v2 хранит отдельный `state\vbios-resolver-cache-v2.txt`.

Это делает метод переносимым на другие VBIOS поддерживаемых RTX 5050/5060/5070/5070 Ti/5080/5090 Laptop, **если в их ROM сохраняется проверенный Blackwell Power Budget v0x40 layout 0x38/0x66**. При другом формате таблицы программа намеренно не угадывает offsets и остаётся fail-closed — такой ROM нужен для добавления нового parser layout.
